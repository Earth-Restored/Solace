package earthrestored.solace.buildplate.connector.plugin;

import org.jetbrains.annotations.NotNull;

import micheal65536.fountain.connector.plugin.ConnectorPlugin;
import micheal65536.fountain.connector.plugin.Inventory;
import earthrestored.solace.buildplate.connector.model.InventoryAddItemMessage;
import earthrestored.solace.buildplate.connector.model.InventoryRemoveItemRequest;
import earthrestored.solace.buildplate.connector.model.InventoryResponse;
import earthrestored.solace.buildplate.connector.model.InventorySetHotbarMessage;
import earthrestored.solace.buildplate.connector.model.InventoryUpdateItemWearMessage;
import earthrestored.solace.eventbus.client.Publisher;
import earthrestored.solace.eventbus.client.RequestSender;

import java.util.Arrays;

final class SyncedPlayerInventory implements PlayerInventory {
	@NotNull
	private final String playerId;
	@NotNull
	private final RequestSender eventBusRequestSender;
	@NotNull
	private final Publisher eventBusPublisher;
	@NotNull
	private final String eventBusQueueName;

	public SyncedPlayerInventory(@NotNull String playerId, @NotNull RequestSender eventBusRequestSender,
			@NotNull Publisher eventBusPublisher, @NotNull String eventBusQueueName) {
		this.playerId = playerId;
		this.eventBusRequestSender = eventBusRequestSender;
		this.eventBusPublisher = eventBusPublisher;
		this.eventBusQueueName = eventBusQueueName;
	}

	@Override
	@NotNull
	public Inventory getContents() throws ConnectorPlugin.ConnectorPluginException {
		InventoryResponse inventoryResponse = EventBusHelper.doRequestResponseSync(this.eventBusRequestSender,
				this.eventBusQueueName, "getInventory", this.playerId, InventoryResponse.class);
		Inventory inventory;
		try {
			inventory = new Inventory(
					Arrays.stream(inventoryResponse.items()).filter(item -> item.instanceId() == null)
							.map(item -> new Inventory.StackableItem(item.id(), item.count()))
							.toArray(Inventory.StackableItem[]::new),
					Arrays.stream(inventoryResponse.items()).filter(item -> item.instanceId() != null)
							.map(item -> new Inventory.NonStackableItem(item.id(), item.instanceId(), item.wear()))
							.toArray(Inventory.NonStackableItem[]::new),
					Arrays.stream(inventoryResponse.hotbar())
							.map(hotbarItem -> hotbarItem != null ? (hotbarItem.instanceId() != null
									? new Inventory.HotbarItem(hotbarItem.id(), hotbarItem.instanceId())
									: new Inventory.HotbarItem(hotbarItem.id(), hotbarItem.count())) : null)
							.toArray(Inventory.HotbarItem[]::new));
		} catch (IllegalArgumentException exception) {
			throw new ConnectorPlugin.ConnectorPluginException("Bad inventory data", exception);
		}
		return inventory;
	}

	@Override
	public void addItem(@NotNull String itemId, int count) throws ConnectorPlugin.ConnectorPluginException {
		EventBusHelper.publishJson(this.eventBusPublisher, this.eventBusQueueName, "inventoryAdd",
				new InventoryAddItemMessage(this.playerId, itemId, count, null, 0));
	}

	@Override
	public void addItem(@NotNull String itemId, @NotNull String instanceId, int wear)
			throws ConnectorPlugin.ConnectorPluginException {
		EventBusHelper.publishJson(this.eventBusPublisher, this.eventBusQueueName, "inventoryAdd",
				new InventoryAddItemMessage(this.playerId, itemId, 1, instanceId, wear));
	}

	@Override
	public int removeItem(@NotNull String itemId, int count) throws ConnectorPlugin.ConnectorPluginException {
		return EventBusHelper.doRequestResponseSync(this.eventBusRequestSender, this.eventBusQueueName,
				"inventoryRemove", new InventoryRemoveItemRequest(this.playerId, itemId, count, null), Integer.class);
	}

	@Override
	public boolean removeItem(@NotNull String itemId, @NotNull String instanceId)
			throws ConnectorPlugin.ConnectorPluginException {
		return EventBusHelper.doRequestResponseSync(this.eventBusRequestSender, this.eventBusQueueName,
				"inventoryRemove", new InventoryRemoveItemRequest(this.playerId, itemId, 1, instanceId), Boolean.class);
	}

	@Override
	public void updateItemWear(@NotNull String itemId, @NotNull String instanceId, int wear)
			throws ConnectorPlugin.ConnectorPluginException {
		EventBusHelper.publishJson(this.eventBusPublisher, this.eventBusQueueName, "inventoryUpdateWear",
				new InventoryUpdateItemWearMessage(this.playerId, itemId, instanceId, wear));
	}

	@Override
	public void setHotbar(Inventory.HotbarItem[] hotbar) throws ConnectorPlugin.ConnectorPluginException {
		EventBusHelper.publishJson(this.eventBusPublisher, this.eventBusQueueName, "inventorySetHotbar",
				new InventorySetHotbarMessage(this.playerId,
						Arrays.stream(hotbar)
								.map(item -> item != null
										? new InventorySetHotbarMessage.Item(item.uuid, item.count, item.instanceId)
										: null)
								.toArray(InventorySetHotbarMessage.Item[]::new)));
	}
}