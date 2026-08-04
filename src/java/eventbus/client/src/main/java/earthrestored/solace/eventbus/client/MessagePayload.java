package earthrestored.solace.eventbus.client;

import java.io.InputStream;

public sealed interface MessagePayload permits
        MessagePayload.StringPayload,
        MessagePayload.BinaryPayload,
        MessagePayload.StreamPayload {

    record StringPayload(String data) implements MessagePayload {
    }

    record BinaryPayload(byte[] data) implements MessagePayload {
    }

    record StreamPayload(InputStream stream) implements MessagePayload {
    }
}
