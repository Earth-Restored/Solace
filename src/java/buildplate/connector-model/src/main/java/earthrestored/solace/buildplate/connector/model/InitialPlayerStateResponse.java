package earthrestored.solace.buildplate.connector.model;

import org.jetbrains.annotations.NotNull;

public record InitialPlayerStateResponse(
		float health,
		@NotNull BoostStatusEffect[] boostStatusEffects) {
	public record BoostStatusEffect(
			@NotNull Type type,
			int value,
			long remainingDuration) {
		public enum Type {
			ADVENTURE_XP,
			DEFENSE,
			EATING,
			HEALTH,
			MINING_SPEED,
			STRENGTH
		}
	}
}