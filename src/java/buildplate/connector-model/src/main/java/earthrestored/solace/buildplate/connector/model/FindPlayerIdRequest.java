package earthrestored.solace.buildplate.connector.model;

import org.jetbrains.annotations.NotNull;

public record FindPlayerIdRequest(
		@NotNull String minecraftId,
		@NotNull String minecraftName) {
}