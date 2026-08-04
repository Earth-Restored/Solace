package earthrestored.solace.buildplate.connector.model;

import org.jetbrains.annotations.NotNull;

public record PlayerConnectedRequest(
		@NotNull String uuid,
		@NotNull String joinCode) {
}