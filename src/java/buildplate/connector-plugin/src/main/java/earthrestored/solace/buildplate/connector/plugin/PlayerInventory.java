package earthrestored.solace.buildplate.connector.plugin;

import org.jetbrains.annotations.NotNull;

import micheal65536.fountain.connector.plugin.ConnectorPlugin;
import micheal65536.fountain.connector.plugin.Inventory;

interface PlayerInventory {
	@NotNull
	Inventory getContents() throws ConnectorPlugin.ConnectorPluginException;

	void addItem(@NotNull String itemId, int count) throws ConnectorPlugin.ConnectorPluginException;

	void addItem(@NotNull String itemId, @NotNull String instanceId, int wear)
			throws ConnectorPlugin.ConnectorPluginException;

	int removeItem(@NotNull String itemId, int count) throws ConnectorPlugin.ConnectorPluginException;

	boolean removeItem(@NotNull String itemId, @NotNull String instanceId)
			throws ConnectorPlugin.ConnectorPluginException;

	void updateItemWear(@NotNull String itemId, @NotNull String instanceId, int wear)
			throws ConnectorPlugin.ConnectorPluginException;

	void setHotbar(Inventory.HotbarItem[] hotbar) throws ConnectorPlugin.ConnectorPluginException;
}