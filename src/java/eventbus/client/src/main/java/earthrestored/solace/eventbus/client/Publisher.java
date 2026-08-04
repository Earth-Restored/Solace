package earthrestored.solace.eventbus.client;

import com.google.protobuf.ByteString;
import com.google.protobuf.UnsafeByteOperations;
import io.grpc.stub.StreamObserver;
import java.io.InputStream;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutorService;

public final class Publisher {
	private final EventBusServiceGrpc.EventBusServiceStub asyncStub;
	private final ExecutorService executor;

	Publisher(EventBusServiceGrpc.EventBusServiceStub asyncStub, ExecutorService executor) {
		this.asyncStub = asyncStub;
		this.executor = executor;
	}

	public CompletableFuture<Boolean> publishAsync(String queueName, String type, String data) {
		PublishRequest req = PublishRequest.newBuilder()
			.setQueueName(queueName)
			.setType(type)
			.setStringData(data)
			.build();
		return publishInternal(req);
	}

	public CompletableFuture<Boolean> publishAsync(String queueName, String type, byte[] data) {
		PublishRequest req = PublishRequest.newBuilder()
			.setQueueName(queueName)
			.setType(type)
			.setBinaryData(UnsafeByteOperations.unsafeWrap(data))
			.build();
		return publishInternal(req);
	}

	public CompletableFuture<Boolean> publishAsync(String queueName, String type, InputStream stream) {
		CompletableFuture<Boolean> future = new CompletableFuture<>();

		StreamObserver<PublishChunk> requestObserver = asyncStub.publishStream(new StreamObserver<>() {
			@Override
			public void onNext(PublishResponse value) {
				future.complete(value.getSuccess());
			}

			@Override
			public void onError(Throwable t) {
				future.completeExceptionally(t);
			}

			@Override
			public void onCompleted() {
			}
		});

		executor.submit(() -> {
			try (stream) {
				requestObserver.onNext(PublishChunk.newBuilder()
					.setMetadata(PublishMetadata.newBuilder().setQueueName(queueName).setType(type).build())
					.build());

				byte[] buffer = new byte[32 * 1024];
				int bytesRead;
				while ((bytesRead = stream.read(buffer)) > 0) {
					requestObserver.onNext(PublishChunk.newBuilder()
						.setChunkData(UnsafeByteOperations.unsafeWrap(buffer, 0, bytesRead))
						.build());
				}
				requestObserver.onCompleted();
			} catch (Exception e) {
				requestObserver.onError(e);
			}
		});

		return future;
	}

	private CompletableFuture<Boolean> publishInternal(PublishRequest request) {
		CompletableFuture<Boolean> future = new CompletableFuture<>();
		asyncStub.publish(request, new StreamObserver<>() {
			@Override
			public void onNext(PublishResponse value) {
				future.complete(value.getSuccess());
			}

			@Override
			public void onError(Throwable t) {
				future.completeExceptionally(t);
			}

			@Override
			public void onCompleted() {
			}
		});
		return future;
	}
}
