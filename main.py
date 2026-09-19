import json
import time
from pathlib import Path

from games.detector import EdenDetector
from games.game_save_reader import GameSaveReader
from games.registry import GameRegistry
from games.state_formatter import GameStateFormatter
from rpc.discord_rpc import DiscordRPC

BASE_DIR = Path(__file__).resolve().parent
CONFIG_PATH = BASE_DIR / "config.json"

BRIDGE_PATH = (
	BASE_DIR
	/ "bridge"
	/ "PokemonSaveReader"
	/ "bin"
	/ "Debug"
	/ "net10.0"
	/ "PokemonSaveReader.dll"
)


def load_config():
	with CONFIG_PATH.open("r", encoding="utf-8") as file:
		return json.load(file)


def main():
	config = load_config()

	client_id = config["discord"]["client_id"]
	save_refresh_interval = config["discord"]["save_refresh_interval"]
	pokedex_rotation_interval = config["discord"]["pokedex_rotation_interval"]

	registry = GameRegistry(config)
	detector = EdenDetector()
	rpc = DiscordRPC(client_id)

	game_save_reader = GameSaveReader(str(BRIDGE_PATH))
	state_formatter = GameStateFormatter()

	current_game_id = None
	previous_eden_running = None

	current_state = None
	previous_rpc_state = None

	pokedex_pages = []
	current_pokedex_page = 0

	last_save_refresh = 0
	last_pokedex_rotation = 0

	print("SWITCH RPC started.")

	try:
		print("Connecting to Discord...")

		if rpc.connect():
			print("Discord RPC connected.")
		else:
			print("Failed to connect to Discord.")

		while True:
			now = time.monotonic()

			eden_running = detector.is_running()
			game_id = detector.detect_game()

			# ---------------------------------------------------------
			# Eden process status
			# ---------------------------------------------------------

			if eden_running != previous_eden_running:
				if eden_running:
					print("Eden: running")
				else:
					print("Eden: not running")

				previous_eden_running = eden_running

			# ---------------------------------------------------------
			# No game detected
			# ---------------------------------------------------------

			if game_id is None:
				if current_game_id is not None:
					print("Game: none")

					rpc.clear()

					current_game_id = None
					current_state = None
					previous_rpc_state = None

					pokedex_pages = []
					current_pokedex_page = 0

					last_save_refresh = 0
					last_pokedex_rotation = 0

				time.sleep(1)
				continue

			# ---------------------------------------------------------
			# Resolve game configuration
			# ---------------------------------------------------------

			game = registry.get(game_id)

			if game is None:
				print(f"No configuration found for: {game_id}")

				rpc.clear()

				current_game_id = None
				current_state = None
				previous_rpc_state = None

				pokedex_pages = []
				current_pokedex_page = 0

				time.sleep(1)
				continue

			# ---------------------------------------------------------
			# Game changed
			# ---------------------------------------------------------

			if game_id != current_game_id:
				print(f"Game detected: {game.name}")

				current_game_id = game_id
				current_state = None
				previous_rpc_state = None

				pokedex_pages = []
				current_pokedex_page = 0

				last_save_refresh = 0
				last_pokedex_rotation = now

			# ---------------------------------------------------------
			# Refresh save data
			# ---------------------------------------------------------

			if now - last_save_refresh >= save_refresh_interval:
				game_state = game_save_reader.read_game(game_id)

				last_save_refresh = now

				if game_state is None:
					print("Failed to read game save.")

					time.sleep(1)
					continue

				pokedex_pages = state_formatter.format_pokedex_pages(game_state)

				if current_pokedex_page >= len(pokedex_pages):
					current_pokedex_page = 0

				current_state = game_state

				print("Save data refreshed.")

			# ---------------------------------------------------------
			# Rotate Pokédex page
			# ---------------------------------------------------------

			if (
				pokedex_pages
				and now - last_pokedex_rotation >= pokedex_rotation_interval
			):
				current_pokedex_page = (current_pokedex_page + 1) % len(
					pokedex_pages
				)

				last_pokedex_rotation = now

			# ---------------------------------------------------------
			# Update Discord RPC only when something changed
			# ---------------------------------------------------------

			if current_state is not None and pokedex_pages:
				pokedex_text = pokedex_pages[current_pokedex_page]

				details = "Pokédex"

				rpc_state = (
					game.name,
					details,
					pokedex_text,
					game.large_image,
					game.large_text,
				)

				if rpc_state != previous_rpc_state:
					if not rpc.connected:
						print("Discord RPC disconnected. Reconnecting...")

						if not rpc.connect():
							print("Failed to connect to Discord.")

							time.sleep(1)
							continue

					success = rpc.update(
						name=game.name,
						details=details,
						state=pokedex_text,
						large_image=game.large_image,
						large_text=game.large_text,
					)

					if success:
						print(
							f"Rich Presence updated: {details} | {pokedex_text}"
						)

						previous_rpc_state = rpc_state
					else:
						print("Failed to update Rich Presence.")

			time.sleep(1)

	except KeyboardInterrupt:
		print("\nStopping SWITCH RPC...")

	finally:
		rpc.clear()
		rpc.close()

		print("RPC cleared.")
		print("Goodbye.")


if __name__ == "__main__":
	main()
