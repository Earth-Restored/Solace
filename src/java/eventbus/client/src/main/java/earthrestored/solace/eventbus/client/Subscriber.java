package earthrestored.solace.eventbus.client;

import io.grpc.stub.StreamObserver;
import java.time.Instant;
import java.time.OffsetDateTime;
import java.time.ZoneId;
import java.util.concurrent.*;

public final class Subscriber implements AutoCloseable {
	private final EventBusServiceGrpc.EventBusServiceStub asyncStub;
	private final String queueName;
	private final SubscriberListener listener;
	private final ExecutorService executor;
	private final Semaphore concurrencySemaphore = new Semaphore(4);

	private final ConcurrentMap<Long, ChunkedInputStream> activeStreams = new ConcurrentHashMap<>();
	private io.grpc.Context.CancellableContext context;

	Subscriber(EventBusServiceGrpc.EventBusServiceStub asyncStub, String queueName,
			SubscriberListener listener, ExecutorService executor) {
		this.asyncStub = asyncStub;
		this.queueName = queueName;
		this.listener = listener;
		this.executor = executor;
	}

	public void start() {
		context = io.grpc.Context.current().withCancellation();
		context.run(() -> {
			asyncStub.subscribe(SubscribeRequest.newBuilder().setQueueName(queueName).build(), new StreamObserver<>() {
				@Override
				public void onNext(EventMessage msg) {
					executor.submit(() -> handleMessage(msg));
				}

				@Override
				public void onError(Throwable t) {
					activeStreams.values().forEach(stream -> stream.error(t));
					listener.error();
				}

				@Override
				public void onCompleted() {
					activeStreams.values().forEach(ChunkedInputStream::complete);
				}
			});
		});
	}

	private void handleMessage(EventMessage msg) {
		OffsetDateTime timestamp = Instant.ofEpochSecond(
				msg.getTimestamp().getSeconds(),
				msg.getTimestamp().getNanos()).atZone(ZoneId.systemDefault()).toOffsetDateTime();

		if (msg.getStreamId() != 0) {
			ChunkedInputStream stream = activeStreams.computeIfAbsent(msg.getStreamId(), id -> {
				ChunkedInputStream newStream = new ChunkedInputStream();
				executor.submit(() -> dispatchEvent(
					new Event(timestamp, msg.getType(), new MessagePayload.StreamPayload(newStream))));
				return newStream;
			});

			if (msg.getPayloadCase() == EventMessage.PayloadCase.BINARY_DATA && !msg.getBinaryData().isEmpty()) {
				stream.push(msg.getBinaryData().toByteArray());
			}
			if (msg.getIsLastChunk()) {
				activeStreams.remove(msg.getStreamId());
				stream.complete();
			}
		} else {
			MessagePayload payload = msg.getPayloadCase() == EventMessage.PayloadCase.BINARY_DATA
				? new MessagePayload.BinaryPayload(msg.getBinaryData().toByteArray())
				: new MessagePayload.StringPayload(msg.getStringData());

			dispatchEvent(new Event(timestamp, msg.getType(), payload));
		}
	}

	private void dispatchEvent(Event event) {
		try {
			concurrencySemaphore.acquire();
			listener.event(event);
		} catch (Exception e) {
			listener.error();
		} finally {
			concurrencySemaphore.release();
		}
	}

	@Override
	public void close() {
		if (context != null)
			context.cancel(null);
		activeStreams.values().forEach(ChunkedInputStream::complete);
	}
}
