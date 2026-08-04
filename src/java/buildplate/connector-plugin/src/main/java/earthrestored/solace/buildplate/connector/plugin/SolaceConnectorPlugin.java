package earthrestored.solace.buildplate.connector.plugin;

import com.google.gson.Gson;
import org.jetbrains.annotations.NotNull;

import micheal65536.fountain.connector.plugin.ConnectorPlugin;
import micheal65536.fountain.connector.plugin.DisconnectResponse;
import micheal65536.fountain.connector.plugin.Inventory;
import micheal65536.fountain.connector.plugin.Logger;
import micheal65536.fountain.connector.plugin.PlayerLoginInfo;
import earthrestored.solace.buildplate.connector.model.ConnectorPluginArg;
import earthrestored.solace.buildplate.connector.model.InventoryResponse;
import earthrestored.solace.buildplate.connector.model.InventoryType;
import earthrestored.solace.buildplate.connector.model.PlayerConnectedRequest;
import earthrestored.solace.buildplate.connector.model.PlayerConnectedResponse;
import earthrestored.solace.buildplate.connector.model.PlayerDisconnectedRequest;
import earthrestored.solace.buildplate.connector.model.PlayerDisconnectedResponse;
import earthrestored.solace.eventbus.client.EventBusClient;
import earthrestored.solace.eventbus.client.Publisher;
import earthrestored.solace.eventbus.client.RequestSender;

import java.util.HashMap;

public final class SolaceConnectorPlugin implements ConnectorPlugin {
	private String queueName;
	private EventBusClient eventBusClient;
	private Publisher publisher;
	private RequestSender requestSender;

	private InventoryType inventoryType;

	private final HashMap<String, PlayerInventory> playerInventories = new HashMap<>();

	@Override
	public void init(@NotNull String arg, @NotNull Logger logger) throws ConnectorPluginException {
		ConnectorPluginArg connectorPluginArg;
		try {
			connectorPluginArg = new Gson().fromJson(arg, ConnectorPluginArg.class);
		} catch (Exception exception) {
			throw new ConnectorPluginException("Invalid connector plugin arg string");
		}

		try {
			this.eventBusClient = EventBusClient.connectAsync(connectorPluginArg.eventBusAddress());
		} catch (Exception exception) {
			throw new ConnectorPluginException(exception);
		}
		this.queueName = connectorPluginArg.eventBusQueueName();
		this.publisher = this.eventBusClient.addPublisher();
		this.requestSender = this.eventBusClient.addRequestSender();

		this.inventoryType = connectorPluginArg.inventoryType();
	}

	@Override
	public void shutdown() throws ConnectorPluginException {
		this.eventBusClient.close();
	}

	@Override
	public boolean onPlayerConnected(@NotNull PlayerLoginInfo playerLoginInfo) throws ConnectorPluginException {
		PlayerConnectedResponse playerConnectedResponse = EventBusHelper.doRequestResponseSync(this.requestSender,
				this.queueName, "playerConnected", new PlayerConnectedRequest(playerLoginInfo.uuid, ""),
				PlayerConnectedResponse.class); // TODO: join code
		if (!playerConnectedResponse.accepted()) {
			return false;
		}
		if (this.inventoryType != InventoryType.SYNCED && playerConnectedResponse.initialInventoryContents() == null) {
			throw new ConnectorPluginException(
					"Bad player connected response data (missing initial inventory data for inventory type %s)"
							.formatted(this.inventoryType.name()));
		}
		this.playerInventories.put(playerLoginInfo.uuid, switch (this.inventoryType) {
			case SYNCED ->
				new SyncedPlayerInventory(playerLoginInfo.uuid, this.requestSender, this.publisher, this.queueName);
			case DISCARD -> new DiscardPlayerInventory(playerConnectedResponse.initialInventoryContents());
			case BACKPACK -> new BackpackPlayerInventory(playerConnectedResponse.initialInventoryContents());
		});
		return true;
	}

	@Override
	@NotNull
	public DisconnectResponse onPlayerDisconnected(@NotNull String playerId, float health)
			throws ConnectorPluginException {
		InventoryResponse backpackContents = switch (this.inventoryType) {
			case SYNCED -> null;
			case DISCARD -> null;
			case BACKPACK ->
				((BackpackPlayerInventory) this.getInventoryForPlayer(playerId)).getContentsAsInventoryResponse();
		};
		PlayerDisconnectedRequest playerDisconnectedRequest = new PlayerDisconnectedRequest(playerId, backpackContents);
		PlayerDisconnectedResponse playerDisconnectedResponse = EventBusHelper.doRequestResponseSync(this.requestSender,
				this.queueName, "playerDisconnected", playerDisconnectedRequest, PlayerDisconnectedResponse.class);
		return new DisconnectResponse();
	}

	@Override
	public boolean onPlayerDead(@NotNull String playerId) throws ConnectorPluginException {
		boolean respawn = EventBusHelper.doRequestResponseSync(this.requestSender, this.queueName, "playerDead",
				playerId, Boolean.class);
		return respawn;
	}

	@Override
	@NotNull
	public Inventory onPlayerGetInventory(@NotNull String playerId) throws ConnectorPluginException {
		return this.getInventoryForPlayer(playerId).getContents();
	}

	@Override
	public void onPlayerInventoryAddItem(@NotNull String playerId, @NotNull String itemId, int count)
			throws ConnectorPluginException {
		this.getInventoryForPlayer(playerId).addItem(itemId, count);
	}

	@Override
	public void onPlayerInventoryAddItem(@NotNull String playerId, @NotNull String itemId, @NotNull String instanceId,
			int wear) throws ConnectorPluginException {
		this.getInventoryForPlayer(playerId).addItem(itemId, instanceId, wear);
	}

	@Override
	public int onPlayerInventoryRemoveItem(@NotNull String playerId, @NotNull String itemId, int count)
			throws ConnectorPluginException {
		return this.getInventoryForPlayer(playerId).removeItem(itemId, count);
	}

	@Override
	public boolean onPlayerInventoryRemoveItem(@NotNull String playerId, @NotNull String itemId,
			@NotNull String instanceId) throws ConnectorPluginException {
		return this.getInventoryForPlayer(playerId).removeItem(itemId, instanceId);
	}

	@Override
	public void onPlayerInventoryUpdateItemWear(@NotNull String playerId, @NotNull String itemId,
			@NotNull String instanceId, int wear) throws ConnectorPluginException {
		this.getInventoryForPlayer(playerId).updateItemWear(itemId, instanceId, wear);
	}

	@Override
	public void onPlayerInventorySetHotbar(@NotNull String playerId, Inventory.HotbarItem[] hotbar)
			throws ConnectorPluginException {
		this.getInventoryForPlayer(playerId).setHotbar(hotbar);
	}

	@NotNull
	private PlayerInventory getInventoryForPlayer(@NotNull String playerId) throws ConnectorPluginException {
		PlayerInventory playerInventory = this.playerInventories.getOrDefault(playerId, null);
		if (playerInventory == null) {
			throw new ConnectorPluginException("Inventory does not exist for player ID (player not connected?)");
		}
		return playerInventory;
	}
}