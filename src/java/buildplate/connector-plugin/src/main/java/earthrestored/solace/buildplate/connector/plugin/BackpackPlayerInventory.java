package earthrestored.solace.buildplate.connector.plugin;

import org.jetbrains.annotations.NotNull;

import micheal65536.fountain.connector.plugin.ConnectorPlugin;
import earthrestored.solace.buildplate.connector.model.InventoryResponse;

import java.util.Arrays;
import java.util.stream.Stream;

final class BackpackPlayerInventory extends LocallyTrackedPlayerInventory {
	public BackpackPlayerInventory(@NotNull InventoryResponse initialContents)
			throws ConnectorPlugin.ConnectorPluginException {
		super(initialContents);
	}

	@NotNull
	public InventoryResponse getContentsAsInventoryResponse() {
		return new InventoryResponse(
				Stream.concat(
						this.stackableItems.entrySet().stream().filter(entry -> entry.getValue() > 0)
								.map(entry -> new InventoryResponse.Item(entry.getKey(), entry.getValue(), null, 0)),
						this.nonStackableItems.entrySet().stream()
								.flatMap(entry -> entry.getValue().entrySet().stream()
										.map(entry1 -> new InventoryResponse.Item(entry.getKey(), 1, entry1.getKey(),
												entry1.getValue()))))
						.toArray(InventoryResponse.Item[]::new),
				Arrays.stream(this.hotbar)
						.map(hotbarItem -> hotbarItem != null
								? new InventoryResponse.HotbarItem(hotbarItem.id(), hotbarItem.count(),
										hotbarItem.instanceId())
								: null)
						.toArray(InventoryResponse.HotbarItem[]::new));
	}
}