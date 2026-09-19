# DISCORD-RPC.md

Dokumentasi integrasi **Discord Rich Presence** pada SWITCH RPC.

Dokumen ini menjelaskan bagaimana aplikasi berkomunikasi dengan Discord, struktur wrapper RPC, lifecycle connection, payload Rich Presence, artwork, troubleshooting, dan aturan pengembangan.

---

## 1. Overview

SWITCH RPC menggunakan Discord Rich Presence untuk menampilkan status game yang sedang dimainkan.

Alur sederhananya:

```text
Eden
  ↓
Game Detector
  ↓
Game Definition
  ↓
GameState
  ↓
DiscordRPC
  ↓
PyPresence
  ↓
Discord IPC
  ↓
Discord Desktop
```

Aplikasi tidak berkomunikasi langsung dengan object PyPresence dari seluruh module. Integrasi Discord dibungkus oleh:

```text
rpc/discord_rpc.py
```

Tujuannya adalah menjaga dependency Discord tetap terisolasi.

---

## 2. Technology

Komponen RPC saat ini:

- Discord Desktop
- Discord Rich Presence / RPC
- PyPresence
- Python
- Discord IPC

Dependency Python:

```text
pypresence==4.6.2
```

Dokumentasi dependency dan API harus diverifikasi terhadap versi yang benar-benar digunakan.

Gunakan Context7 dan dokumentasi resmi ketika:

- mengubah payload;
- mengubah lifecycle connection;
- menggunakan API PyPresence baru;
- terjadi perubahan versi;
- debugging Discord RPC.

---

## 3. Discord Application

Rich Presence membutuhkan Discord Application.

Application menyediakan:

- Client/Application ID;
- Rich Presence assets;
- konfigurasi aplikasi Discord.

Client ID digunakan oleh aplikasi untuk membuka koneksi RPC.

Contoh konfigurasi:

```json
{
	"discord": {
		"client_id": "YOUR_DISCORD_APPLICATION_ID",
		"update_interval": 15
	}
}
```

Jangan memasukkan token Discord atau credential privat ke konfigurasi repository.

Client/Application ID sendiri bukan secret seperti password atau token, tetapi tetap sebaiknya dikonfigurasi secara sadar.

---

## 4. RPC Wrapper

File utama:

```text
rpc/discord_rpc.py
```

Class:

```python
DiscordRPC
```

Wrapper menangani:

```text
connect()
update()
clear()
close()
```

Lifecycle:

```text
Disconnected
     ↓
connect()
     ↓
Connected
     ↓
update()
     ↓
Connected
     ↓
clear()
     ↓
close()
     ↓
Disconnected
```

Jika connection gagal saat runtime:

```text
Connected
     ↓
update() fails
     ↓
mark disconnected
     ↓
next update cycle
     ↓
reconnect
```

---

## 5. Connection Lifecycle

### Connect

Aplikasi tidak membuat connection baru pada setiap polling cycle.

Connection dibuat ketika diperlukan:

```python
if rpc.connect():
	print("Discord RPC connected.")
```

Wrapper menyimpan status:

```python
self.connected
```

dan object RPC:

```python
self.rpc
```

Session start time juga disimpan:

```python
self.start_time
```

---

## 6. Elapsed Time

RPC menggunakan timestamp start untuk menampilkan elapsed session time.

Saat berhasil connect:

```python
self.start_time = int(time.time())
```

Timestamp tersebut dikirim pada update:

```python
start = self.start_time
```

Hasilnya Discord dapat menampilkan durasi sejak RPC session dimulai.

Catatan:

- timestamp berasal dari runtime aplikasi;
- restart aplikasi akan membuat session timestamp baru;
- perpindahan game dapat memerlukan keputusan terpisah mengenai apakah timer harus reset.

---

## 7. Rich Presence Payload

Payload saat ini memiliki konsep:

```text
name
details
state
large_image
large_text
start
```

Contoh:

```text
Name:
Pokémon Legends: Arceus

Details:
Exploring Hisui

State:
Pokédex: 8 / 242
```

Artinya:

```text
name        → nama game
details     → aktivitas/konteks
state       → informasi tambahan
large_image → artwork game
large_text  → tooltip artwork
start       → elapsed time
```

---

## 8. Game Name

Nama game berasal dari `GameDefinition`.

Contoh:

```python
game.name
```

Konfigurasi:

```json
{
	"pokemon_legends_arceus": {
		"name": "Pokémon Legends: Arceus"
	}
}
```

Jangan hardcode nama game di `DiscordRPC`.

RPC wrapper seharusnya menerima data yang akan ditampilkan, bukan menentukan game apa yang sedang dimainkan.

---

## 9. Details

Details dibentuk oleh application layer.

Contoh saat ini:

```python
details = f"Exploring {game.region}"
```

Untuk Legends: Arceus:

```text
Exploring Hisui
```

Untuk Scarlet/Violet:

```text
Exploring Paldea
```

Jika nantinya location dari save reader sudah tersedia, layer application dapat mengubah details menjadi informasi lokasi yang lebih spesifik.

---

## 10. State

State digunakan untuk informasi tambahan.

Contoh:

```text
Pokédex: 25 / 1025
```

atau jika data belum tersedia:

```text
Pokédex: —
```

Data state sebaiknya berasal dari `GameState`, bukan dari Discord wrapper.

Contoh konsep:

```text
Save Reader
    ↓
GameState
    ↓
RPC payload
```

---

## 11. Artwork

Artwork disimpan sebagai Discord Rich Presence asset.

Game configuration menentukan key artwork:

```json
{
	"pokemon_legends_arceus": {
		"large_image": "arceus",
		"large_text": "Pokémon Legends: Arceus"
	}
}
```

Contoh:

```text
arceus
scarlet
violet
za
```

Key tersebut harus cocok dengan asset yang tersedia pada Discord Application.

Jika asset key tidak cocok, artwork tidak akan ditampilkan sesuai harapan.

---

## 12. Artwork Naming

Gunakan nama asset yang:

- sederhana;
- stabil;
- lowercase jika memungkinkan;
- tidak bergantung pada filename lokal;
- tidak berubah hanya karena perubahan display name.

Contoh:

```text
arceus
scarlet
violet
za
```

Jangan menggunakan path lokal:

```text
D:\Games\...
```

Artwork Discord disimpan pada Discord Application, bukan pada filesystem project.

---

## 13. Update Interval

Polling interval dikonfigurasi melalui:

```json
{
	"discord": {
		"update_interval": 15
	}
}
```

Nilai ini menentukan seberapa sering application loop melakukan pemeriksaan.

Contoh:

```text
15 seconds
    ↓
detect Eden
    ↓
detect game
    ↓
read/update state bila diperlukan
    ↓
update RPC bila diperlukan
    ↓
wait
```

Interval yang terlalu kecil dapat menyebabkan:

- process invocation berlebihan;
- filesystem scan berlebihan;
- save parsing terlalu sering;
- RPC update terlalu sering.

Karena itu update frequency harus tetap masuk akal.

---

## 14. Update Only When Needed

Application sebaiknya tidak melakukan update Discord tanpa alasan.

Contoh perubahan penting:

```text
No game
  ↓
Arceus
```

RPC perlu di-update.

Contoh:

```text
Arceus
  ↓
No game
```

RPC perlu di-clear.

Jika nantinya state berubah:

```text
Pokédex: 8 / 242
  ↓
Pokédex: 9 / 242
```

RPC dapat di-update.

Namun save reader tidak harus dipanggil pada setiap loop jika data belum membutuhkan refresh.

---

## 15. Reconnection

Discord dapat tidak tersedia ketika aplikasi mulai dijalankan.

Contoh:

```text
Application starts
        ↓
Discord unavailable
        ↓
RPC connection fails
        ↓
Application keeps running
        ↓
Discord becomes available
        ↓
next required update
        ↓
reconnect
```

Connection failure tidak seharusnya membuat aplikasi crash.

Wrapper menangani error dan mengubah:

```python
self.connected = False
```

kemudian connection dapat dibuat kembali.

---

## 16. Clear RPC

Ketika game berhenti, RPC harus dibersihkan.

Contoh:

```python
rpc.clear()
```

Ini penting agar Discord tidak terus menampilkan game setelah game sudah ditutup.

Lifecycle aplikasi:

```text
Game running
    ↓
RPC active
    ↓
Game closes
    ↓
RPC clear
```

Saat application sendiri dihentikan, `finally` juga harus membersihkan RPC.

---

## 17. Close vs Clear

Keduanya memiliki tujuan berbeda.

### `clear()`

Menghapus Rich Presence aktif.

```python
rpc.clear()
```

### `close()`

Menutup koneksi RPC.

```python
rpc.close()
```

Urutan shutdown:

```text
clear()
  ↓
close()
```

Jangan menganggap `close()` otomatis berarti presence sudah dibersihkan dalam semua kondisi.

---

## 18. Error Handling

RPC wrapper harus menangani error eksternal.

Contoh kegagalan:

- Discord tidak berjalan;
- IPC unavailable;
- connection dropped;
- payload ditolak;
- Discord RPC error.

Application sebaiknya tetap berjalan jika RPC gagal.

Contoh:

```python
try:
	...
except Exception as error:
	print(f"RPC update failed: {error}")
```

Namun jangan menggunakan exception handling untuk menyembunyikan programming error tanpa logging.

---

## 19. Discord IPC

Discord Desktop menyediakan IPC endpoint yang digunakan oleh library RPC.

Pada Windows, PyPresence menggunakan Discord IPC mechanism.

Developer tidak perlu membuat named pipe/IPC protocol sendiri selama PyPresence menyediakan abstraction yang dibutuhkan.

Jika IPC troubleshooting diperlukan, periksa environment Discord dan dokumentasi PyPresence sebelum membuat implementasi custom.

---

## 20. Testing RPC

Testing minimal:

### Test 1 — Discord tersedia

1. Buka Discord Desktop.
2. Jalankan aplikasi.
3. Pastikan connection berhasil.
4. Jalankan Eden.
5. Jalankan game.
6. Periksa Rich Presence.

### Test 2 — Game berhenti

1. RPC sedang aktif.
2. Tutup game.
3. Pastikan detector tidak lagi menemukan game.
4. Pastikan RPC di-clear.

### Test 3 — Discord restart

1. Jalankan application.
2. Pastikan RPC aktif.
3. Tutup Discord.
4. Amati connection failure.
5. Buka Discord kembali.
6. Pastikan application dapat reconnect.

### Test 4 — Ganti game

1. Jalankan game A.
2. Pastikan RPC menampilkan game A.
3. Tutup game A.
4. Jalankan game B.
5. Pastikan RPC berubah ke game B.

---

## 21. Debugging Checklist

Jika RPC tidak muncul:

### Discord

- [ ] Discord Desktop sedang berjalan.
- [ ] User login ke Discord.
- [ ] Discord Application tersedia.
- [ ] Client ID benar.

### Python

- [ ] Virtual environment aktif.
- [ ] PyPresence terinstall.
- [ ] Versi dependency sesuai `requirements.txt`.

Periksa:

```powershell
python -m pip show pypresence
```

### Application

- [ ] `rpc.connect()` berhasil.
- [ ] `rpc.connected == True`.
- [ ] game terdeteksi.
- [ ] `rpc.update()` berhasil.

### Artwork

- [ ] asset tersedia di Discord Application.
- [ ] asset key sama dengan `large_image`.
- [ ] artwork sudah selesai diproses Discord.

---

## 22. Common Failure: RPC Tidak Connect

Gejala:

```text
Failed to connect to Discord.
```

Periksa:

1. Discord Desktop;
2. Client ID;
3. PyPresence;
4. IPC;
5. application configuration.

Jangan langsung mengubah code connection sebelum memastikan environment Discord normal.

---

## 23. Common Failure: RPC Connect Tetapi Tidak Tampil

Kemungkinan:

- game belum terdeteksi;
- update belum dipanggil;
- payload gagal;
- Discord application salah;
- artwork key salah.

Lihat log:

```text
Discord RPC connected.
Game detected: Pokémon Scarlet
Rich Presence updated.
```

Jika `Rich Presence updated.` muncul tetapi artwork bermasalah, fokuskan debugging pada asset configuration, bukan connection.

---

## 24. Common Failure: Timer Tidak Sesuai

Timer menggunakan `start_time` dari RPC connection.

Jika timer ingin merepresentasikan waktu bermain game secara akurat, application perlu menentukan kapan session dimulai.

Misalnya:

```text
Eden starts
    ↓
Game detected
    ↓
start session
```

bukan:

```text
Application starts
    ↓
Discord connects
    ↓
wait for game
```

Keputusan ini merupakan bagian dari application/session state, bukan tanggung jawab PyPresence.

---

## 25. Future RPC Features

Fitur yang dapat ditambahkan:

- actual location;
- actual Pokédex progress;
- caught count;
- playtime dari save;
- party Pokémon;
- current map;
- badges/progression;
- game-specific details;
- dynamic artwork;
- party Pokémon icons;
- trainer name.

Setiap fitur harus:

1. memiliki sumber data yang jelas;
2. berasal dari save reader atau runtime source yang terverifikasi;
3. dimasukkan ke `GameState`;
4. kemudian dipetakan ke RPC.

---

## 26. Jangan Masukkan Game Logic ke RPC

Hindari:

```python
class DiscordRPC:
	def update_scarlet(self): ...
```

atau:

```python
if game_id == "pokemon_scarlet":
	...
```

di dalam wrapper RPC.

Gunakan:

```text
Game detection
      ↓
Game definition
      ↓
Game state
      ↓
RPC payload
```

RPC hanya bertugas mengirim data.

---

## 27. API Isolation

Dependency PyPresence sebaiknya hanya digunakan di:

```text
rpc/discord_rpc.py
```

Module lain tidak perlu melakukan:

```python
from pypresence import Presence
```

Dengan isolation ini, jika library diganti di masa depan, perubahan dapat difokuskan pada satu layer.

---

## 28. Configuration Rules

Jangan hardcode:

- Client ID;
- update interval;
- game display name;
- region;
- artwork key.

Data tersebut berada di:

```text
config.json
```

Contoh:

```json
{
	"discord": {
		"client_id": "YOUR_DISCORD_APPLICATION_ID",
		"update_interval": 15
	},
	"games": {
		"pokemon_scarlet": {
			"name": "Pokémon Scarlet",
			"region": "Paldea",
			"large_image": "scarlet",
			"large_text": "Pokémon Scarlet"
		}
	}
}
```

---

## 29. Development Rules

Saat mengubah Discord RPC:

- gunakan wrapper yang sudah ada;
- jangan menyebarkan PyPresence ke module lain;
- cek dokumentasi versi dependency;
- gunakan Context7 bila relevan;
- pertahankan reconnect behavior;
- pertahankan shutdown cleanup;
- jangan membuat connection pada setiap polling cycle;
- jangan memasukkan game-specific logic ke RPC wrapper;
- test Discord Desktop secara nyata.

---

## 30. Security

Jangan menyimpan:

- Discord token;
- user token;
- private credentials;
- secret API keys;

di source code atau repository.

Client/Application ID bukan credential login Discord, tetapi tetap jangan menambahkan credential lain secara sembarangan.

---

## 31. Related Documentation

- `README.md`
- `AGENTS.md`
- `docs/ARCHITECTURE.md`
- `docs/DEVELOPMENT.md`
- `docs/CONFIGURATION.md`
- `docs/SAVE-READER.md`
- `docs/GAME-SUPPORT.md`
- `docs/TROUBLESHOOTING.md`
- `docs/ROADMAP.md`

---

## 32. Reference

Dokumentasi eksternal yang relevan:

- Discord Rich Presence documentation
- Discord RPC documentation
- PyPresence documentation/source
- Context7 documentation

Selalu gunakan dokumentasi yang sesuai dengan versi dependency yang sedang digunakan.
