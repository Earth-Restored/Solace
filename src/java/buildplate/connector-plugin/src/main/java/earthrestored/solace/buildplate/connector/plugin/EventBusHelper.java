package earthrestored.solace.buildplate.connector.plugin;

import com.google.gson.Gson;
import org.jetbrains.annotations.NotNull;

import micheal65536.fountain.connector.plugin.ConnectorPlugin;
import earthrestored.solace.eventbus.client.MessagePayload;
import earthrestored.solace.eventbus.client.Publisher;
import earthrestored.solace.eventbus.client.RequestSender;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutionException;

public final class EventBusHelper {
	public static void publishJson(@NotNull Publisher publisher, @NotNull String queueName, @NotNull String type,
			Object messageObject) throws ConnectorPlugin.ConnectorPluginException {
		CompletableFuture<Boolean> completableFuture = publisher.publishAsync(queueName, type,
				new Gson().newBuilder().serializeNulls().create().toJson(messageObject));
		boolean success;
		for (;;) {
			try {
				success = completableFuture.get();
				break;
			} catch (ExecutionException exception) {
				throw new AssertionError(exception);
			} catch (InterruptedException exception) {
				continue;
			}
		}
		if (!success) {
			throw new ConnectorPlugin.ConnectorPluginException();
		}
	}

	public static <T> T receiveJson(@NotNull String messageString, @NotNull Class<T> messageClass)
			throws ConnectorPlugin.ConnectorPluginException {
		try {
			return new Gson().fromJson(messageString, messageClass);
		} catch (Exception exception) {
			throw new ConnectorPlugin.ConnectorPluginException(exception);
		}
	}

	@NotNull
	public static <T> CompletableFuture<T> doRequestResponse(@NotNull RequestSender requestSender,
			@NotNull String queueName, @NotNull String requestType, Object requestObject,
			@NotNull Class<T> responseClass) {
		CompletableFuture<T> completableFuture = new CompletableFuture<>();

		Gson gson = new Gson().newBuilder().serializeNulls().create();
		requestSender.requestAsync(queueName, requestType, gson.toJson(requestObject)).thenAccept(responseMessage -> {
			if (responseMessage == null) {
				completableFuture.complete(null);
			} else {
				try {
					T response = new Gson().fromJson(((MessagePayload.StringPayload) responseMessage).data(),
							responseClass);
					completableFuture.complete(response);
				} catch (Exception exception) {
					completableFuture.complete(null);
				}
			}
		});

		return completableFuture;
	}

	@NotNull
	public static <T> T doRequestResponseSync(@NotNull RequestSender requestSender, @NotNull String queueName,
			@NotNull String requestType, Object requestObject, @NotNull Class<T> responseClass)
			throws ConnectorPlugin.ConnectorPluginException {
		CompletableFuture<T> completableFuture = EventBusHelper.doRequestResponse(requestSender, queueName, requestType,
				requestObject, responseClass);
		for (;;) {
			try {
				T response = completableFuture.get();
				if (response == null) {
					throw new ConnectorPlugin.ConnectorPluginException();
				}
				return response;
			} catch (ExecutionException exception) {
				throw new AssertionError(exception);
			} catch (InterruptedException exception) {
				continue;
			}
		}
	}
}