package earthrestored.solace.buildplate.connector.plugin;

import org.jetbrains.annotations.NotNull;

import micheal65536.fountain.connector.plugin.ConnectorPlugin;
import earthrestored.solace.buildplate.connector.model.InventoryResponse;

final class DiscardPlayerInventory extends LocallyTrackedPlayerInventory {
	public DiscardPlayerInventory(@NotNull InventoryResponse initialContents)
			throws ConnectorPlugin.ConnectorPluginException {
		super(initialContents);
	}
}