package earthrestored.solace.eventbus.client;

import org.jetbrains.annotations.NotNull;

public interface SubscriberListener {
    void event(@NotNull Event event);
    void error();
}
