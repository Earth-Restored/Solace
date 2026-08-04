package earthrestored.solace.buildplate.connector.model;

import org.jetbrains.annotations.NotNull;

public record WorldSavedMessage(
		@NotNull String dataBase64) {
}