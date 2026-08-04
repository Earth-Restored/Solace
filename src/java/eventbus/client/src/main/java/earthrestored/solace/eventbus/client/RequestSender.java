package earthrestored.solace.eventbus.client;

import com.google.protobuf.UnsafeByteOperations;
import io.grpc.Status;
import io.grpc.StatusRuntimeException;
import io.grpc.stub.StreamObserver;

import java.io.InputStream;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.atomic.AtomicBoolean;

public final class RequestSender {
	private final EventBusServiceGrpc.EventBusServiceStub asyncStub;
	private final ExecutorService executor;

	RequestSender(EventBusServiceGrpc.EventBusServiceStub asyncStub, ExecutorService executor) {
		this.asyncStub = asyncStub;
		this.executor = executor;
	}

	public CompletableFuture<MessagePayload> requestAsync(String queueName, String type, String data) {
		RequestMessage request = RequestMessage.newBuilder()
			.setQueueName(queueName)
			.setType(type)
			.setStringData(data)
			.build();
		return requestInternalAsync(request);
	}

	public CompletableFuture<MessagePayload> requestAsync(String queueName, String type, byte[] data) {
		RequestMessage request = RequestMessage.newBuilder()
			.setQueueName(queueName)
			.setType(type)
			.setBinaryData(UnsafeByteOperations.unsafeWrap(data))
			.build();
		return requestInternalAsync(request);
	}

	public CompletableFuture<MessagePayload> requestAsync(String queueName, String type, InputStream stream) {
		CompletableFuture<MessagePayload> future = new CompletableFuture<>();

		StreamObserver<RequestChunk> requestObserver = asyncStub.requestStream(new StreamObserver<>() {
			private final AtomicBoolean firstMessageReceived = new AtomicBoolean(false);
			private ChunkedInputStream chunkedResponseStream;

			@Override
			public void onNext(ResponseChunk chunk) {
				if (firstMessageReceived.compareAndSet(false, true)) {
					if (chunk.getStatus() == ResponseMessage.Status.NoHandlers) {
						future.complete(null);
						return;
					}

					if (chunk.getStatus() != ResponseMessage.Status.Success) {
						String errorMsg = chunk.getErrorMessage().isEmpty()
								? "Request failed with status: " + chunk.getStatus()
								: chunk.getErrorMessage();
						future.completeExceptionally(new RuntimeException(errorMsg));
						return;
					}

					if (chunk.getIsStream()) {
						chunkedResponseStream = new ChunkedInputStream();
						if (chunk.getPayloadCase() == ResponseChunk.PayloadCase.BINARY_DATA
								&& !chunk.getBinaryData().isEmpty()) {
							chunkedResponseStream.push(chunk.getBinaryData().toByteArray());
						}
						if (chunk.getIsLastChunk()) {
							chunkedResponseStream.complete();
						}
						future.complete(new MessagePayload.StreamPayload(chunkedResponseStream));
					} else {
						MessagePayload payload = switch (chunk.getPayloadCase()) {
							case BINARY_DATA -> new MessagePayload.BinaryPayload(chunk.getBinaryData().toByteArray());
							case STRING_DATA -> new MessagePayload.StringPayload(chunk.getStringData());
							default -> null;
						};
						future.complete(payload);
					}
				} else {
					if (chunkedResponseStream != null) {
						if (chunk.getPayloadCase() == ResponseChunk.PayloadCase.BINARY_DATA
								&& !chunk.getBinaryData().isEmpty()) {
							chunkedResponseStream.push(chunk.getBinaryData().toByteArray());
						}
						if (chunk.getIsLastChunk()) {
							chunkedResponseStream.complete();
						}
					}
				}
			}

			@Override
			public void onError(Throwable t) {
				if (!future.isDone()) {
					future.completeExceptionally(t);
				}
				if (chunkedResponseStream != null) {
					chunkedResponseStream.error(t);
				}
			}

			@Override
			public void onCompleted() {
				if (!future.isDone()) {
					future.completeExceptionally(new StatusRuntimeException(
							Status.INTERNAL.withDescription("No response received from server.")));
				}
				if (chunkedResponseStream != null) {
					chunkedResponseStream.complete();
				}
			}
		});

		executor.submit(() -> {
			try (stream) {
				requestObserver.onNext(RequestChunk.newBuilder()
					.setMetadata(RequestMetadata.newBuilder()
						.setQueueName(queueName)
						.setType(type)
						.setIsStream(true)
						.build())
					.build());

				byte[] currentBuffer = new byte[32 * 1024];
				int currentBytes = stream.read(currentBuffer);

				while (currentBytes > 0) {
					byte[] nextBuffer = new byte[32 * 1024];
					int nextBytes = stream.read(nextBuffer);

					boolean isLast = (nextBytes <= 0);

					requestObserver.onNext(RequestChunk.newBuilder()
						.setIsLastChunk(isLast)
						.setChunkData(UnsafeByteOperations.unsafeWrap(currentBuffer, 0, currentBytes))
						.build());

					currentBuffer = nextBuffer;
					currentBytes = nextBytes;
				}

				requestObserver.onCompleted();
			} catch (Exception e) {
				requestObserver.onError(e);
			}
		});

		return future;
	}

	private CompletableFuture<MessagePayload> requestInternalAsync(RequestMessage requestMessage) {
		CompletableFuture<MessagePayload> future = new CompletableFuture<>();

		asyncStub.request(requestMessage, new StreamObserver<>() {
			@Override
			public void onNext(ResponseMessage response) {
				switch (response.getStatus()) {
					case Success -> {
						MessagePayload payload = response.getPayloadCase() == ResponseMessage.PayloadCase.BINARY_DATA
							? new MessagePayload.BinaryPayload(response.getBinaryData().toByteArray())
							: new MessagePayload.StringPayload(response.getStringData());
						future.complete(payload);
					}
					case NoHandlers -> future.complete(null);
					default -> future.completeExceptionally(new RuntimeException(response.getErrorMessage()));
				}
			}

			@Override
			public void onError(Throwable t) {
				future.completeExceptionally(t);
			}

			@Override
			public void onCompleted() {
				if (!future.isDone()) {
					future.complete(null);
				}
			}
		});

		return future;
	}
}