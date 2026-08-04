package earthrestored.solace.eventbus.client;

import java.time.OffsetDateTime;

public record Event(OffsetDateTime timestamp, String type, MessagePayload data) {
}
