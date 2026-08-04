package earthrestored.solace.buildplate.connector.model;

import org.jetbrains.annotations.NotNull;

public record InventoryUpdateItemWearMessage(
		@NotNull String playerId,
		@NotNull String itemId,
		@NotNull String instanceId,
		int wear) {
}