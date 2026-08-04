package earthrestored.solace.eventbus.client;

import io.grpc.ManagedChannel;
import io.grpc.ManagedChannelBuilder;

import java.net.URI;
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
		ManagedChannel channel = createChannel(connectionString);
		var asyncStub = EventBusServiceGrpc.newStub(channel);
		return new EventBusClient(channel, asyncStub);
	}

	private static ManagedChannel createChannel(String connectionString) {
		String normalizedConnectionString = normalizeConnectionString(connectionString);

		return ManagedChannelBuilder.forTarget("dns:///" + normalizedConnectionString)
				.usePlaintext()
				.build();
	}

	static String normalizeConnectionString(String connectionString) {
		if (connectionString == null || connectionString.isBlank()) {
			throw new IllegalArgumentException("connectionString must not be null or blank");
		}

		URI uri;
		try {
			uri = URI.create(connectionString);
		} catch (IllegalArgumentException ex) {
			return connectionString;
		}

		if (uri.getHost() == null || uri.getPort() == -1) {
			return connectionString;
		}

		return uri.getHost() + ":" + uri.getPort();
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
