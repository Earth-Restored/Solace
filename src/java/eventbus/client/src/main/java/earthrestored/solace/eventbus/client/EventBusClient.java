package earthrestored.solace.eventbus.client;

import io.grpc.ManagedChannel;
import io.grpc.ManagedChannelBuilder;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public final class EventBusClient implements AutoCloseable {
	private final ManagedChannel channel;
	private final EventBusServiceGrpc.EventBusServiceStub asyncStub;

	private final ExecutorService virtualExecutor = Executors.newVirtualThreadPerTaskExecutor();

	private EventBusClient(ManagedChannel channel, EventBusServiceGrpc.EventBusServiceStub asyncStub) {
		this.channel = channel;
		this.asyncStub = asyncStub;
	}

	public static EventBusClient connectAsync(String connectionString) {
		ManagedChannel channel = ManagedChannelBuilder.forTarget(connectionString)
			.usePlaintext()
			.build();

		var asyncStub = EventBusServiceGrpc.newStub(channel);
		return new EventBusClient(channel, asyncStub);
	}

	public Publisher addPublisher() {
		return new Publisher(asyncStub, virtualExecutor);
	}

	public Subscriber addSubscriber(String queueName, SubscriberListener listener) {
		Subscriber subscriber = new Subscriber(asyncStub, queueName, listener, virtualExecutor);
		subscriber.start();
		return subscriber;
	}

	public RequestSender addRequestSender() {
		return new RequestSender(asyncStub, virtualExecutor);
	}

	public RequestHandler addRequestHandler(String queueName, RequestListener listener) {
		RequestHandler handler = new RequestHandler(asyncStub, queueName, listener, virtualExecutor);
		handler.start();
		return handler;
	}

	@Override
	public void close() {
		virtualExecutor.shutdownNow();
		channel.shutdown();
	}
}
