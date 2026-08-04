package earthrestored.solace.eventbus.client;

import java.time.OffsetDateTime;

public record RequestEvent(OffsetDateTime timestamp, String type, MessagePayload data) {
}
