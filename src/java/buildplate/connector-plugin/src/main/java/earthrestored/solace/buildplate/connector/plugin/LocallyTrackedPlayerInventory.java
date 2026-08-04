package earthrestored.solace.buildplate.connector.plugin;

import org.jetbrains.annotations.NotNull;

import micheal65536.fountain.connector.plugin.ConnectorPlugin;
import micheal65536.fountain.connector.plugin.Inventory;
import earthrestored.solace.buildplate.connector.model.InventoryResponse;

import java.util.Arrays;
import java.util.HashMap;
import java.util.HashSet;

abstract class LocallyTrackedPlayerInventory implements PlayerInventory {
	// TODO: this class does not perform any validation, need to determine how much
	// validation is required in this context

	protected final HashMap<String, Integer> stackableItems = new HashMap<>();
	protected final HashMap<String, HashMap<String, Integer>> nonStackableItems = new HashMap<>();
	protected final InventoryResponse.HotbarItem[] hotbar = new InventoryResponse.HotbarItem[7];

	protected LocallyTrackedPlayerInventory(@NotNull InventoryResponse initialContents)
			throws ConnectorPlugin.ConnectorPluginException {
		for (InventoryResponse.Item item : initialContents.items()) {
			if (item.instanceId() == null) {
				this.stackableItems.put(item.id(), this.stackableItems.getOrDefault(item.id(), 0) + item.count());
			} else {
				this.nonStackableItems.computeIfAbsent(item.id(), itemId -> new HashMap<>()).put(item.instanceId(),
						item.wear());
			}
		}
		for (int index = 0; index < this.hotbar.length; index++) {
			InventoryResponse.HotbarItem hotbarItem = initialContents.hotbar()[index];
			this.hotbar[index] = hotbarItem != null
					? new InventoryResponse.HotbarItem(hotbarItem.id(), hotbarItem.count(), hotbarItem.instanceId())
					: null;
		}
	}

	protected LocallyTrackedPlayerInventory() {
		// empty
	}

	@Override
	@NotNull
	public final Inventory getContents() throws ConnectorPlugin.ConnectorPluginException {
		return new Inventory(
				this.stackableItems.entrySet().stream().filter(entry -> entry.getValue() > 0)
						.map(entry -> new Inventory.StackableItem(entry.getKey(), entry.getValue()))
						.toArray(Inventory.StackableItem[]::new),
				this.nonStackableItems.entrySet().stream().flatMap(entry -> entry.getValue().entrySet().stream().map(
						entry1 -> new Inventory.NonStackableItem(entry.getKey(), entry1.getKey(), entry1.getValue())))
						.toArray(Inventory.NonStackableItem[]::new),
				Arrays.stream(this.hotbar)
						.map(hotbarItem -> hotbarItem != null ? (hotbarItem.instanceId() == null
								? new Inventory.HotbarItem(hotbarItem.id(), hotbarItem.count())
								: new Inventory.HotbarItem(hotbarItem.id(), hotbarItem.instanceId())) : null)
						.toArray(Inventory.HotbarItem[]::new));
	}

	@Override
	public final void addItem(@NotNull String itemId, int count) throws ConnectorPlugin.ConnectorPluginException {
		this.stackableItems.put(itemId, this.stackableItems.getOrDefault(itemId, 0) + count);
	}

	@Override
	public final void addItem(@NotNull String itemId, @NotNull String instanceId, int wear)
			throws ConnectorPlugin.ConnectorPluginException {
		this.nonStackableItems.computeIfAbsent(itemId, itemId1 -> new HashMap<>()).put(instanceId, wear);
	}

	@Override
	public final int removeItem(@NotNull String itemId, int count) throws ConnectorPlugin.ConnectorPluginException {
		count = Math.min(count, this.stackableItems.getOrDefault(itemId, 0));
		this.stackableItems.put(itemId, this.stackableItems.getOrDefault(itemId, 0) - count);
		this.limitHotbarToInventory();
		return count;
	}

	@Override
	public final boolean removeItem(@NotNull String itemId, @NotNull String instanceId)
			throws ConnectorPlugin.ConnectorPluginException {
		HashMap<String, Integer> instances = this.nonStackableItems.getOrDefault(itemId, null);
		if (instances == null) {
			return false;
		}
		boolean success = instances.remove(instanceId) != null;
		this.limitHotbarToInventory();
		return success;
	}

	@Override
	public final void updateItemWear(@NotNull String itemId, @NotNull String instanceId, int wear)
			throws ConnectorPlugin.ConnectorPluginException {
		HashMap<String, Integer> instances = this.nonStackableItems.getOrDefault(itemId, null);
		if (instances == null) {
			return;
		}
		if (!instances.containsKey(instanceId)) {
			return;
		}
		instances.put(instanceId, wear);
	}

	@Override
	public final void setHotbar(Inventory.HotbarItem[] hotbar) throws ConnectorPlugin.ConnectorPluginException {
		for (int index = 0; index < this.hotbar.length; index++) {
			Inventory.HotbarItem hotbarItem = hotbar[index];
			this.hotbar[index] = hotbarItem != null
					? new InventoryResponse.HotbarItem(hotbarItem.uuid, hotbarItem.count, hotbarItem.instanceId)
					: null;
		}
		this.limitHotbarToInventory();
	}

	private void limitHotbarToInventory() {
		HashMap<String, Integer> hotbarItemCounts = new HashMap<>();
		HashMap<String, HashSet<String>> hotbarItemInstances = new HashMap<>();
		for (int index = 0; index < 7; index++) {
			InventoryResponse.HotbarItem hotbarItem = this.hotbar[index];
			if (hotbarItem != null) {
				if (hotbarItem.instanceId() == null) {
					int availableCount = this.stackableItems.getOrDefault(hotbarItem.id(), 0)
							- hotbarItemCounts.getOrDefault(hotbarItem.id(), 0);
					hotbarItemCounts.put(hotbarItem.id(),
							hotbarItemCounts.getOrDefault(hotbarItem.id(), 0) + hotbarItem.count());
					if (hotbarItem.count() > availableCount) {
						if (availableCount <= 0) {
							hotbarItem = null;
						} else {
							hotbarItem = new InventoryResponse.HotbarItem(hotbarItem.id(), availableCount, null);
						}
					}
				} else {
					HashSet<String> hotbarInstances = hotbarItemInstances.computeIfAbsent(hotbarItem.id(),
							uuid -> new HashSet<>());
					if (!hotbarInstances.add(hotbarItem.instanceId())) {
						hotbarItem = null;
					} else {
						HashMap<String, Integer> instances = this.nonStackableItems.getOrDefault(hotbarItem.id(), null);
						if (instances == null) {
							hotbarItem = null;
						} else if (!instances.containsKey(hotbarItem.instanceId())) {
							hotbarItem = null;
						}
					}
				}
				this.hotbar[index] = hotbarItem;
			}
		}
	}
}