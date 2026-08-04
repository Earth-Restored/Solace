package earthrestored.solace.buildplate.connector.model;

import org.jetbrains.annotations.Nullable;

public record PlayerConnectedResponse(
		boolean accepted,
		@Nullable InventoryResponse initialInventoryContents) {
}