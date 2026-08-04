package earthrestored.solace.buildplate.connector.model;

import org.jetbrains.annotations.NotNull;
import org.jetbrains.annotations.Nullable;

public record InventorySetHotbarMessage(
		@NotNull String playerId,
		Item[] items) {
	public record Item(
			@NotNull String itemId,
			int count,
			@Nullable String instanceId) {
	}
}