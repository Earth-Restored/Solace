package earthrestored.solace.buildplate.connector.model;

import org.jetbrains.annotations.NotNull;

public record ConnectorPluginArg(
		@NotNull String eventBusAddress,
		@NotNull String eventBusQueueName,
		@NotNull InventoryType inventoryType) {
}