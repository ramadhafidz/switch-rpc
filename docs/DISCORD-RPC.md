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
Game Detection
  ↓
Game Definition
  ↓
GameState
  ↓
PresenceClient
  ↓
DiscordRichPresence library
  ↓
Discord IPC
  ↓
Discord Desktop
```

Aplikasi tidak memanggil object library Discord dari seluruh project. Integrasi Discord dibungkus oleh:

```text
src/SwitchRpc.Discord/PresenceClient.cs
```

Tujuannya menjaga dependency Discord tetap terisolasi — hanya project ini yang menyentuh tipe library Discord.

---

## 2. Technology

Komponen RPC saat ini:

- Discord Desktop
- Discord Rich Presence / RPC
- Library **DiscordRichPresence** (NuGet, oleh Lachee)
- .NET 10
- Discord IPC

Catatan penting: package NuGet yang benar adalah **`DiscordRichPresence`**; package bernama `DiscordRPC` di NuGet adalah library lain yang berbeda. Namespace kode tetap `DiscordRPC`.

Dokumentasi dependency dan API harus diverifikasi terhadap versi yang benar-benar digunakan (dokumentasi XML ikut terdistribusi bersama package).

---

## 3. Discord Application

Rich Presence membutuhkan Discord Application.

Application menyediakan:

- Client/Application ID;
- Rich Presence assets;
- konfigurasi aplikasi Discord.

Client ID digunakan oleh aplikasi untuk membuka koneksi RPC, dimuat dari `config.json`.

Jangan memasukkan token Discord atau credential privat ke konfigurasi repository.

Client/Application ID sendiri bukan secret seperti password atau token, tetapi tetap sebaiknya dikonfigurasi secara sadar.

---

## 4. RPC Wrapper

File utama:

```text
src/SwitchRpc.Discord/PresenceClient.cs
```

Class:

```csharp
PresenceClient
```

Wrapper menangani:

```text
Connect()
Update()
Clear()
Dispose()
```

Lifecycle:

```text
Disconnected
     ↓
Connect()
     ↓
Connected
     ↓
Update()
     ↓
Connected
     ↓
Clear()
     ↓
Dispose()
     ↓
Disconnected
```

Connection state dilacak melalui event library (`OnReady`, `OnClose`, `OnConnectionFailed`, `OnError`), bukan hanya dari hasil `Connect()`.

---

## 5. Connection Lifecycle

### Connect

Aplikasi tidak membuat connection baru pada setiap polling cycle.

Connection dibuat ketika diperlukan, dan gagalnya koneksi tidak membuat aplikasi berhenti:

```text
Application starts
        ↓
Discord unavailable
        ↓
Connect() returns false
        ↓
Application keeps running
        ↓
next required update
        ↓
reconnect attempt
```

### Reconnect

Setelah pipa IPC mati (misalnya Discord ditutup di tengah jalan), library tetap menandai dirinya sebagai initialized. Karena itu `Connect()` memanggil `Deinitialize()` sebelum `Initialize()` lagi.

`SetPresence` bersifat fire-and-forget — ia hanya mengantre pesan. Kegagalan pipa terdeteksi secara asinkron lewat event, jadi ada jeda beberapa detik antara Discord ditutup dan status wrapper berubah.

---

## 6. Elapsed Time

RPC menggunakan timestamp start untuk menampilkan elapsed session time.

Timestamp disimpan saat connect pertama berhasil dan **dipertahankan** saat reconnect — timer di Discord tidak ikut reset ketika koneksi sempat terputus.

Timestamp tersebut dikirim pada setiap update melalui `Timestamps`.

Catatan:

- timestamp berasal dari runtime aplikasi;
- restart aplikasi akan membuat session timestamp baru;
- perpindahan game dapat memerlukan keputusan terpisah mengenai apakah timer harus reset.

---

## 7. Rich Presence Payload

Payload saat ini memiliki konsep:

```text
details
state
large_image
large_text
timestamps
```

Contoh tampilan:

```text
Details:
Pokédex

State:
Paldea: 22/400

Artwork:
scarlet (tooltip "Pokémon Scarlet")
```

Artinya:

```text
details      → konteks aktivitas
state        → informasi tambahan (halaman Pokédex)
large_image  → artwork game
large_text   → tooltip artwork
timestamps   → elapsed session time
```

---

## 8. Data Source

Data yang ditampilkan berasal dari `GameState`, dibentuk oleh `PresenceFormatter` di `SwitchRpc.Core`:

```text
Save Reader
    ↓
GameState
    ↓
PresenceFormatter
    ↓
RPC payload
```

Jangan hardcode nama game, artwork, atau data save di dalam wrapper RPC.

RPC wrapper seharusnya menerima data yang akan ditampilkan, bukan menentukan game apa yang sedang dimainkan.

---

## 9. Artwork

Artwork disimpan sebagai Discord Rich Presence asset.

Game configuration menentukan key artwork:

```json
{
	"pokemon_scarlet": {
		"large_image": "scarlet",
		"large_text": "Pokémon Scarlet"
	}
}
```

Contoh key:

```text
arceus
scarlet
violet
za
```

Key tersebut harus cocok dengan asset yang tersedia pada Discord Application.

Jika asset key tidak cocok, artwork tidak akan ditampilkan sesuai harapan.

### Artwork Naming

Gunakan nama asset yang:

- sederhana;
- stabil;
- lowercase jika memungkinkan;
- tidak bergantung pada filename lokal;
- tidak berubah hanya karena perubahan display name.

Jangan menggunakan path lokal — artwork Discord disimpan pada Discord Application, bukan pada filesystem project.

---

## 10. Update Interval

Dua interval dikonfigurasi melalui `config.json`:

```json
{
	"discord": {
		"save_refresh_interval": 15,
		"pokedex_rotation_interval": 5
	}
}
```

- `save_refresh_interval` — seberapa sering save dibaca ulang;
- `pokedex_rotation_interval` — seberapa sering halaman Pokédex berganti.

Per tick, loop melakukan:

```text
detect Eden + game
    ↓
read/save refresh bila jatuh tempo
    ↓
rotate dex page bila jatuh tempo
    ↓
update RPC bila tampilan berubah
    ↓
wait
```

Interval yang terlalu kecil dapat menyebabkan filesystem scan berlebihan, save parsing terlalu sering, dan RPC update terlalu sering.

---

## 11. Update Only When Needed

Application sebaiknya tidak melakukan update Discord tanpa alasan.

Contoh perubahan penting:

```text
No game  →  Scarlet    → RPC perlu di-update
Scarlet  →  No game    → RPC perlu di-clear
Paldea: 22/400  →  Kitakami: 3/200    → RPC perlu di-update
```

Aplikasi membandingkan tampilan berikutnya dengan yang terakhir terkirim; update hanya dikirim saat ada perbedaan.

---

## 12. Clear RPC

Ketika game berhenti, RPC harus dibersihkan.

```csharp
_rpc.Clear();
```

Ini penting agar Discord tidak terus menampilkan game setelah game sudah ditutup.

Saat aplikasi sendiri dihentikan, blok `finally` juga harus membersihkan RPC sebelum `Dispose()`.

### Clear vs Dispose

Keduanya memiliki tujuan berbeda.

### `Clear()`

Menghapus Rich Presence aktif.

### `Dispose()`

Menutup koneksi RPC dan melepaskan resource.

Urutan shutdown:

```text
Clear()
  ↓
Dispose()
```

---

## 13. Error Handling

RPC wrapper harus menangani error eksternal.

Contoh kegagalan:

- Discord tidak berjalan;
- IPC unavailable;
- connection dropped;
- payload ditolak;
- Discord RPC error.

Application sebaiknya tetap berjalan jika RPC gagal — kegagalan di-log dan status koneksi diperbarui, lalu update berikutnya mencoba reconnect.

Namun jangan menggunakan exception handling untuk menyembunyikan programming error tanpa logging.

---

## 14. Discord IPC

Discord Desktop menyediakan IPC endpoint (named pipe) yang digunakan oleh library RPC.

Developer tidak perlu membuat named pipe/IPC protocol sendiri selama library menyediakan abstraction yang dibutuhkan.

Jika IPC troubleshooting diperlukan, periksa environment Discord dan dokumentasi library sebelum membuat implementasi custom.

---

## 15. Testing RPC

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
3. Tutup Discord (quit, bukan minimize).
4. Amati `Discord RPC disconnected. Reconnecting...` dan kegagalan sementara.
5. Buka Discord kembali.
6. Pastikan application reconnect dan presence kembali dengan timer yang tidak reset.

### Test 4 — Ganti game

1. Jalankan game A.
2. Pastikan RPC menampilkan game A.
3. Tutup game A.
4. Jalankan game B.
5. Pastikan RPC berubah ke game B.

---

## 16. Debugging Checklist

Jika RPC tidak muncul:

### Discord

- [ ] Discord Desktop sedang berjalan.
- [ ] User login ke Discord.
- [ ] Discord Application tersedia.
- [ ] Client ID benar.

### Application

- [ ] `Connect()` berhasil.
- [ ] `IsConnected == true`.
- [ ] game terdeteksi.
- [ ] `Update()` berhasil.

### Artwork

- [ ] asset tersedia di Discord Application.
- [ ] asset key sama dengan `large_image`.
- [ ] artwork sudah selesai diproses Discord.

---

## 17. Common Failure: RPC Tidak Connect

Gejala:

```text
Failed to connect to Discord.
```

Periksa:

1. Discord Desktop;
2. Client ID;
3. IPC;
4. application configuration.

Jangan langsung mengubah code connection sebelum memastikan environment Discord normal.

---

## 18. Common Failure: RPC Connect Tetapi Tidak Tampil

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
Rich Presence updated: Pokédex | Paldea: 22/400
```

Jika `Rich Presence updated.` muncul tetapi artwork bermasalah, fokuskan debugging pada asset configuration, bukan connection.

---

## 19. Common Failure: Timer Tidak Sesuai

Timer menggunakan timestamp dari koneksi RPC pertama.

Timestamp dipertahankan lintas reconnect, sehingga durasi yang tampil adalah durasi sesi aplikasi berjalan.

Jika perilaku ini ingin diubah (misalnya timer per game), keputusan itu adalah bagian dari application/session state di `AppLoop`, bukan tanggung jawab wrapper Discord.

---

## 20. Jangan Masukkan Game Logic ke RPC

Hindari method atau branch seperti:

```csharp
public void UpdateScarlet() { ... }

if (gameId == "pokemon_scarlet") { ... }
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

## 21. API Isolation

Library DiscordRichPresence sebaiknya hanya digunakan di:

```text
src/SwitchRpc.Discord/
```

Project lain tidak perlu melakukan:

```csharp
using DiscordRPC;
```

Dengan isolation ini, jika library diganti di masa depan, perubahan dapat difokuskan pada satu project.

---

## 22. Configuration Rules

Jangan hardcode:

- Client ID;
- interval;
- game display name;
- region;
- artwork key.

Data tersebut berada di `config.json` — lihat `docs/CONFIGURATION.md`.

---

## 23. Development Rules

Saat mengubah Discord RPC:

- gunakan wrapper yang sudah ada;
- jangan menyebarkan library Discord ke project lain;
- cek dokumentasi versi dependency;
- pertahankan reconnect behavior;
- pertahankan shutdown cleanup;
- jangan membuat connection pada setiap polling cycle;
- jangan memasukkan game-specific logic ke RPC wrapper;
- test Discord Desktop secara nyata.

---

## 24. Security

Jangan menyimpan:

- Discord token;
- user token;
- private credentials;
- secret API keys;

di source code atau repository.

Client/Application ID bukan credential login Discord, tetapi tetap jangan menambahkan credential lain secara sembarangan.

---

## 25. Future RPC Features

Fitur yang dapat ditambahkan:

- party Pokémon;
- current map;
- badges/progression;
- game-specific details;
- dynamic artwork;
- trainer name.

Setiap fitur harus:

1. memiliki sumber data yang jelas;
2. berasal dari save reader atau runtime source yang terverifikasi;
3. dimasukkan ke `GameState`;
4. kemudian dipetakan ke RPC.

---

## 26. Related Documentation

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

## 27. Reference

Dokumentasi eksternal yang relevan:

- Discord Rich Presence documentation
- DiscordRichPresence library documentation (XML docs terdistribusi bersama package)
- Discord IPC documentation

Selalu gunakan dokumentasi yang sesuai dengan versi dependency yang sedang digunakan.
