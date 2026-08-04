package earthrestored.solace.eventbus.client;

import com.google.protobuf.UnsafeByteOperations;
import io.grpc.stub.StreamObserver;
import java.io.InputStream;
import java.time.Instant;
import java.time.OffsetDateTime;
import java.time.ZoneId;
import java.util.concurrent.*;

public final class RequestHandler implements AutoCloseable {
	private final EventBusServiceGrpc.EventBusServiceStub asyncStub;
	private final String queueName;
	private final RequestListener listener;
	private final ExecutorService executor;
	private final Semaphore concurrencySemaphore = new Semaphore(4);

	private final ConcurrentMap<Long, ChunkedInputStream> activeStreams = new ConcurrentHashMap<>();
	private StreamObserver<ClientMessage> safeWriter;
	private io.grpc.Context.CancellableContext context;

	RequestHandler(EventBusServiceGrpc.EventBusServiceStub asyncStub, String queueName,
			RequestListener listener, ExecutorService executor) {
		this.asyncStub = asyncStub;
		this.queueName = queueName;
		this.listener = listener;
		this.executor = executor;
	}

	public void start() {
		context = io.grpc.Context.current().withCancellation();
		context.run(() -> {
			StreamObserver<ClientMessage> requestObserver = asyncStub.handleRequests(new StreamObserver<>() {
				@Override
				public void onNext(ServerMessage msg) {
					executor.submit(() -> handleServerMessage(msg));
				}

				@Override
				public void onError(Throwable t) {
					listener.error();
				}

				@Override
				public void onCompleted() {
				}
			});

			safeWriter = new StreamObserver<>() {
				@Override
				public synchronized void onNext(ClientMessage value) {
					requestObserver.onNext(value);
				}

				@Override
				public synchronized void onError(Throwable t) {
					requestObserver.onError(t);
				}

				@Override
				public synchronized void onCompleted() {
					requestObserver.onCompleted();
				}
			};

			safeWriter.onNext(ClientMessage.newBuilder().setRegisterQueue(queueName).build());
		});
	}

	private void handleServerMessage(ServerMessage msg) {
		OffsetDateTime timestamp = Instant.ofEpochSecond(
			msg.getTimestamp().getSeconds(),
			msg.getTimestamp().getNanos()).atZone(ZoneId.systemDefault()).toOffsetDateTime();

		if (msg.getIsStream()) {
			ChunkedInputStream stream = activeStreams.computeIfAbsent(msg.getCorrelationId(), id -> {
				ChunkedInputStream newStream = new ChunkedInputStream();
				executor.submit(() -> dispatchRequest(msg,
					new RequestEvent(timestamp, msg.getType(), new MessagePayload.StreamPayload(newStream))));
				return newStream;
			});

			if (msg.getPayloadCase() == ServerMessage.PayloadCase.BINARY_DATA && !msg.getBinaryData().isEmpty()) {
				stream.push(msg.getBinaryData().toByteArray());
			}
			if (msg.getIsLastChunk()) {
				activeStreams.remove(msg.getCorrelationId());
				stream.complete();
			}
		} else {
			MessagePayload payload = msg.getPayloadCase() == ServerMessage.PayloadCase.BINARY_DATA
				? new MessagePayload.BinaryPayload(msg.getBinaryData().toByteArray())
				: new MessagePayload.StringPayload(msg.getStringData());
			dispatchRequest(msg, new RequestEvent(timestamp, msg.getType(), payload));
		}
	}

	private void dispatchRequest(ServerMessage msg, RequestEvent event) {
		try {
			concurrencySemaphore.acquire();
			MessagePayload responsePayload = listener.request(event);

			if (responsePayload == null) {
				safeWriter.onNext(ClientMessage.newBuilder()
					.setResponse(HandlerResponse.newBuilder()
						.setCorrelationId(msg.getCorrelationId())
						.setStatus(HandlerResponse.Status.NotHandled).build())
					.build());
				return;
			}

			// Pattern Matching for Switch (Java 21)
			switch (responsePayload) {
				case MessagePayload.StringPayload s ->
					safeWriter.onNext(ClientMessage.newBuilder()
						.setResponse(HandlerResponse.newBuilder()
							.setCorrelationId(msg.getCorrelationId())
							.setStatus(HandlerResponse.Status.Success)
							.setStringData(s.data()).build())
						.build());

				case MessagePayload.BinaryPayload b ->
					safeWriter.onNext(ClientMessage.newBuilder()
						.setResponse(HandlerResponse.newBuilder()
							.setCorrelationId(msg.getCorrelationId())
							.setStatus(HandlerResponse.Status.Success)
							.setBinaryData(UnsafeByteOperations.unsafeWrap(b.data())).build())
						.build());

				case MessagePayload.StreamPayload st -> {
					try (InputStream is = st.stream()) {
						byte[] buffer = new byte[32 * 1024];
						int bytesRead;
						while ((bytesRead = is.read(buffer)) > 0) {
							boolean isLast = is.available() == 0;
							safeWriter.onNext(ClientMessage.newBuilder()
								.setResponse(HandlerResponse.newBuilder()
									.setCorrelationId(msg.getCorrelationId())
									.setStatus(HandlerResponse.Status.Success)
									.setIsStream(true)
									.setIsLastChunk(isLast)
									.setBinaryData(UnsafeByteOperations.unsafeWrap(buffer, 0, bytesRead))
									.build())
								.build());
						}
					}
				}
			}
		} catch (Exception e) {
			listener.error();
			safeWriter.onNext(ClientMessage.newBuilder()
				.setResponse(HandlerResponse.newBuilder()
					.setCorrelationId(msg.getCorrelationId())
					.setStatus(HandlerResponse.Status.Error).build())
				.build());
		} finally {
			concurrencySemaphore.release();
		}
	}

	@Override
	public void close() {
		if (context != null)
			context.cancel(null);
		if (safeWriter != null)
			safeWriter.onCompleted();
	}
}
