package earthrestored.solace.eventbus.client;

import org.jetbrains.annotations.NotNull;

public interface RequestListener {
    MessagePayload request(@NotNull RequestEvent event);
    void error();
}
