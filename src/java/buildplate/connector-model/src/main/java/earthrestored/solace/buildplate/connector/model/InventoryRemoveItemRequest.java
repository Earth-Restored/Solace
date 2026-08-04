package earthrestored.solace.buildplate.connector.model;

import org.jetbrains.annotations.NotNull;
import org.jetbrains.annotations.Nullable;

public record InventoryRemoveItemRequest(
		@NotNull String playerId,
		@NotNull String itemId,
		int count,
		@Nullable String instanceId) {
}