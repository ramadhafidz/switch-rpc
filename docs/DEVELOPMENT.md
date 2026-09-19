# DEVELOPMENT.md

Panduan pengembangan untuk **SWITCH RPC**.

Dokumen ini menjelaskan workflow development, struktur kode, cara menjalankan komponen, testing, debugging, dan aturan praktis ketika menambahkan atau mengubah fitur.

---

## 1. Tujuan Dokumen

Gunakan dokumen ini sebagai panduan ketika:

- menyiapkan environment development;
- menjalankan aplikasi secara lokal;
- mengembangkan detector, save reader, atau Discord RPC;
- menambahkan dukungan game baru;
- menjalankan test dan debugging;
- memperbarui dependency;
- memeriksa perubahan sebelum commit.

Untuk aturan yang harus diikuti oleh AI coding agent, lihat `AGENTS.md`.

Dokumentasi arsitektur tersedia di:

- `docs/ARCHITECTURE.md`
- `docs/SAVE-READER.md`
- `docs/GAME-SUPPORT.md`
- `docs/CONFIGURATION.md`
- `docs/BENCHMARKS.md`

---

## 2. Prasyarat

Environment utama proyek saat ini adalah:

- Windows 11
- .NET SDK 10+
- Discord Desktop
- Nintendo Switch Eden emulator
- Git

Komponen yang membutuhkan environment Windows:

- Eden process detection;
- window title detection;
- Discord IPC;
- pengujian runtime dengan game berjalan.

---

## 3. Setup Repository

Clone repository:

```powershell
git clone <repository-url>
cd switch-rpc
```

---

## 4. PKHeX Local Setup

Source PKHeX digunakan sebagai dependency lokal dan di-referensikan langsung oleh project `SwitchRpc.Games.Pokemon`.

Struktur yang diharapkan:

```text
bridge/
└── PKHeX/
    └── PKHeX.Core/
```

PKHeX **tidak disimpan sebagai bagian dari repository utama** dan harus tetap di-ignore oleh Git.

Letakkan source PKHeX di `bridge/PKHeX/` sehingga ProjectReference berikut dapat di-resolve:

```text
src/SwitchRpc.Games.Pokemon/SwitchRpc.Games.Pokemon.csproj
  → ..\..\bridge\PKHeX\PKHeX.Core\PKHeX.Core.csproj
```

---

## 5. Dokumentasi Dependency

Ketika membutuhkan dokumentasi library, framework, SDK, atau tool eksternal:

1. periksa source lokal yang benar-benar di-build;
2. dokumentasi resmi;
3. sumber teknis terpercaya lainnya.

Untuk PKHeX, source code lokal adalah referensi penting. Jika dokumentasi eksternal berbeda dengan versi yang digunakan, prioritaskan API yang benar-benar tersedia pada source lokal.

**Jangan mengarang API, property, offset save, atau struktur data.**

---

## 6. Build dan Menjalankan Aplikasi

Build seluruh solusi:

```powershell
dotnet build SwitchRpc.slnx
```

Jalankan aplikasi:

```powershell
dotnet run --project src/SwitchRpc.App
```

Output normal kira-kira:

```text
SWITCH RPC started.
Connecting to Discord...
Discord RPC connected.
Eden: running
Game detected: Pokémon Scarlet
Save data refreshed.
Rich Presence updated: Pokédex | Paldea: 22/400
```

Aplikasi berjalan sebagai polling loop dan memeriksa status Eden secara berkala. Interval diatur melalui `config.json`.

### Mode Diagnose

Menjalankan seluruh pipeline satu kali tanpa membutuhkan Eden atau Discord, dan mencetak metrik pipeline (dipakai untuk `docs/BENCHMARKS.md`):

```powershell
dotnet run --project src/SwitchRpc.App --no-build -- --diagnose
```

---

## 7. Development Workflow

Workflow yang disarankan:

```text
Understand
   ↓
Inspect existing code
   ↓
Verify external APIs against local source
   ↓
Implement smallest change
   ↓
Run focused test
   ↓
Run full test suite
   ↓
Review diff
   ↓
Update documentation
   ↓
Commit
```

Jangan langsung melakukan refactor besar ketika masalah dapat diselesaikan dengan perubahan kecil.

---

## 8. Struktur Project

| Project | Tanggung jawab |
|---|---|
| `src/SwitchRpc.Core` | `GameState`, `DexStats`, `GameDefinition`, presence formatting — tanpa dependency |
| `src/SwitchRpc.Games.Pokemon` | Save reader Pokémon (PKHeX) di balik facade `PokemonSaveReader` |
| `src/SwitchRpc.Emulators.Eden` | Deteksi proses/window Eden dan lokasi save |
| `src/SwitchRpc.Discord` | Wrapper Discord Rich Presence |
| `src/SwitchRpc.App` | Host console: loop, konfigurasi, diagnose |
| `tests/SwitchRpc.Tests` | Test xUnit |

Aturan dependency yang ditegakkan:

- `SwitchRpc.Core` tidak mereferensikan apa pun;
- hanya `SwitchRpc.Games.Pokemon` yang menyentuh PKHeX;
- hanya `SwitchRpc.Discord` yang menyentuh library Discord;
- definisi game dimuat dari `config.json`.

---

## 9. C# Coding Rules

### Indentasi

Gunakan **tab** untuk indentasi (mengikuti kode yang ada).

### General Rules

- PascalCase untuk type dan member, camelCase untuk lokal;
- record untuk data immutable (`GameState`, `GameDefinition`);
- fitur modern C# ketika sesuai (collection expressions, pattern matching);
- hindari hardcoded path milik developer;
- exception handling pada boundary eksternal;
- jangan menelan error secara diam-diam jika error tersebut penting untuk debugging;
- pertahankan project tetap kecil dan fokus.

---

## 10. Save Reader Development

Save reader adalah bagian yang paling sensitif terhadap perubahan format game.

Aturan utama:

- read-only;
- jangan menulis kembali save;
- jangan mengubah save;
- jangan menebak offset;
- jangan menebak struktur binary;
- gunakan abstraction PKHeX jika tersedia;
- verifikasi dengan save nyata;
- dokumentasikan hasil verifikasi.

Semua interaksi PKHeX harus tetap di dalam `SwitchRpc.Games.Pokemon`, di balik facade `PokemonSaveReader`. Hasil akhirnya adalah `GameState` yang ternormalisasi — tipe PKHeX tidak boleh menyeberang keluar project ini.

Untuk detail, lihat `docs/SAVE-READER.md`.

---

## 11. Menambahkan Data Save Baru

Jika ingin menambahkan field baru:

### Langkah 1 — Cari abstraction

Cari apakah PKHeX sudah menyediakan property, save block, accessor, helper, enum, atau method yang sesuai.

### Langkah 2 — Verifikasi source

Pastikan API tersebut tersedia pada versi PKHeX yang sedang digunakan (`bridge/PKHeX/`).

### Langkah 3 — Tambahkan ke reader

Tambahkan ekstraksi di `SvSaveReader` / `PlaSaveReader` (atau reader game lain), lalu petakan ke `GameState`.

### Langkah 4 — Build dan Test

```powershell
dotnet build SwitchRpc.slnx
dotnet test SwitchRpc.slnx
```

### Langkah 5 — Verifikasi dengan save nyata

```powershell
dotnet run --project src/SwitchRpc.App --no-build -- --diagnose
```

---

## 12. Game Detection Development

Detector menggunakan:

1. process detection (`Process.GetProcessesByName("eden")`);
2. window title Eden (Win32 `EnumWindows`);
3. pencocokan judul window terhadap nama game dari `config.json`.

Jika menambahkan game baru:

- tambahkan konfigurasi game (`name`, `title_id`, artwork);
- gunakan stable internal ID;
- verifikasi dengan Eden yang benar-benar menjalankan game tersebut.

Contoh ID:

```text
pokemon_legends_arceus
pokemon_scarlet
pokemon_violet
pokemon_legends_za
```

---

## 13. Discord RPC Development

Discord RPC harus tetap menjadi layer terpisah.

`PresenceClient` bertanggung jawab untuk:

- connect;
- update;
- clear;
- dispose;
- melacak status koneksi melalui event library (`OnReady`, `OnClose`, `OnConnectionFailed`, `OnError`);
- deinitialize + initialize ulang saat reconnect.

Game logic tidak boleh bergantung langsung pada tipe library Discord.

Jika library berubah:

1. cek dokumentasi versi yang digunakan;
2. cek source/package;
3. ubah wrapper `PresenceClient`;
4. hindari menyebarkan API library ke seluruh project.

---

## 14. Testing

```powershell
dotnet test SwitchRpc.slnx
```

Test xUnit berada di `tests/SwitchRpc.Tests`.

Cakupan saat ini:

- `PresenceFormatterTests` — format halaman Pokédex;
- `SvSaveReaderTests` / `PlaSaveReaderTests` — reader terhadap save blank PKHeX (in-memory);
- `PokemonSaveReaderTests` — identifikasi file dan hasil untuk format yang tidak didukung;
- `EdenSaveLocatorTests` — resolusi path save dengan root yang di-inject.

Catatan penting: save blank PKHeX **tidak dapat** di-round-trip melalui `SaveUtil.GetSaveFile` karena identifikasi gen9 menyertakan fingerprint ukuran file on-disk. Karena itu test reader memakai objek `SaveFile` in-memory, sedangkan identifikasi file di-cover oleh test garbage-file dan verifikasi save nyata.

### Integration Test

Jalankan:

```powershell
dotnet run --project src/SwitchRpc.App
```

kemudian:

1. buka Discord;
2. jalankan Eden;
3. jalankan salah satu game;
4. pastikan game terdeteksi;
5. pastikan RPC muncul dan berotasi;
6. tutup game;
7. pastikan RPC dibersihkan.

---

## 15. Save Tanpa Menulis Save

Save test harus selalu bersifat read-only.

Gunakan copy save jika eksperimen membutuhkan inspeksi tambahan.

Jangan menjalankan kode yang:

- memanggil save writer;
- menyimpan perubahan;
- memodifikasi byte;
- mengubah Pokémon;
- mengubah Pokédex;
- mengubah progress.

Tujuan project adalah membaca state game untuk Rich Presence.

---

## 16. Debugging

Gunakan debugging bertahap.

### Eden tidak terdeteksi

Periksa:

```powershell
Get-Process eden -ErrorAction SilentlyContinue
```

### Game tidak terdeteksi

Pastikan:

- Eden sedang berjalan;
- game benar-benar aktif;
- window title mengandung nama game yang dikonfigurasi;
- konfigurasi game di `config.json` benar.

### Discord tidak terhubung

Periksa:

- Discord Desktop sedang berjalan;
- Client/Application ID benar;
- Discord IPC tersedia.

Jangan membuat koneksi RPC baru setiap polling cycle — wrapper menangani reconnect.

### Save tidak ditemukan

Periksa:

- Eden save root;
- `title_id` di `config.json`;
- folder game;
- file `main`;
- permission;
- apakah game sudah pernah membuat save.

### Save gagal dibaca

Jalankan diagnose:

```powershell
dotnet run --project src/SwitchRpc.App --no-build -- --diagnose
```

Pisahkan masalah menjadi:

```text
Path
 ↓
File
 ↓
PKHeX identification
 ↓
Save parsing
 ↓
GameState
```

---

## 17. Dependency Changes

Sebelum mengubah dependency:

1. cek versi saat ini;
2. cek dokumentasi;
3. cek breaking changes;
4. cek compatibility;
5. lakukan perubahan kecil;
6. jalankan test;
7. update dokumentasi jika diperlukan.

Jangan upgrade semua dependency sekaligus tanpa alasan.

Gunakan `dotnet --info` untuk membantu reproduksi environment.

---

## 18. Git Workflow

Sebelum commit:

```powershell
git status
```

Review perubahan:

```powershell
git diff
git diff --cached
git diff --cached --name-only
```

Jangan commit:

- `bin/`, `obj/`;
- save file;
- emulator data;
- `bridge/PKHeX/`;
- secret;
- local configuration;
- cache.

Gunakan `.gitignore` sebagai perlindungan tambahan, tetapi tetap review `git status`.

---

## 19. Commit

Gunakan commit yang menjelaskan perubahan.

Contoh:

```text
feat(save-reader): support Legends Z-A saves
```

Jenis commit yang dapat digunakan:

```text
feat:
fix:
refactor:
docs:
test:
chore:
```

Hindari commit seperti:

```text
update
fix
changes
test
asdf
```

---

## 20. Menambahkan Game Baru

Workflow:

```text
1. Tentukan stable game ID
        ↓
2. Tambahkan konfigurasi game (name, title_id, artwork)
        ↓
3. Verifikasi deteksi window title
        ↓
4. Verifikasi save location melalui title ID
        ↓
5. Verifikasi save format
        ↓
6. Implementasi save reader (bila format didukung PKHeX)
        ↓
7. Mapping ke GameState
        ↓
8. Test save
        ↓
9. Test Eden + Discord RPC
        ↓
10. Update documentation
```

Jangan menyatakan game "supported" hanya karena detector sudah mengenal nama game.

Detection support dan save-reader support adalah dua hal berbeda.

---

## 21. Performance

Polling harus tetap ringan.

Prinsip:

- jangan membaca save setiap detik;
- jangan membuat process baru tanpa kebutuhan;
- jangan reconnect Discord secara terus-menerus;
- gunakan interval konfigurasi;
- hindari scan filesystem berlebihan;
- satu process scan per tick (`EdenDetector.Poll()`).

Jika save reader membutuhkan waktu cukup lama, jangan menjalankannya lebih sering daripada yang diperlukan untuk memperbarui Rich Presence.

---

## 22. Privacy dan Security

Project membaca data lokal dari emulator.

Jangan log atau expose data sensitif tanpa kebutuhan.

Hindari memasukkan ke repository:

- save file;
- trainer ID jika tidak diperlukan;
- user-specific absolute paths;
- Discord credentials;
- API keys;
- tokens;
- environment secrets.

Jika contoh membutuhkan data sensitif, gunakan placeholder.

---

## 23. Checklist Sebelum Pull Request

### Code

- [ ] Perubahan sesuai tanggung jawab project.
- [ ] Tidak ada hardcoded user path.
- [ ] Tidak ada API yang diada-adakan.
- [ ] Error handling cukup.
- [ ] Tidak ada save modification.

### Save Reader

- [ ] Format save telah diverifikasi.
- [ ] API PKHeX telah diverifikasi.
- [ ] Tidak ada guessed offset.
- [ ] Save tetap read-only.
- [ ] Tipe PKHeX tidak keluar dari `SwitchRpc.Games.Pokemon`.

### Runtime

- [ ] Eden detection bekerja.
- [ ] Game detection bekerja.
- [ ] Discord RPC bekerja.
- [ ] RPC dibersihkan ketika game berhenti.

### Git

- [ ] `git status` bersih dari file lokal yang tidak seharusnya di-commit.
- [ ] Tidak ada save file.
- [ ] Tidak ada PKHeX source.
- [ ] Tidak ada build output.
- [ ] Tidak ada secret.

### Documentation

- [ ] README masih akurat.
- [ ] Dokumentasi terkait sudah diperbarui.
- [ ] Status game support sesuai implementasi sebenarnya.

---

## 24. Prinsip Development

Urutan prioritas ketika membuat perubahan:

1. **Correctness**
2. **Read-only safety**
3. **Maintainability**
4. **Modularity**
5. **Performance**
6. **User experience**

Untuk save parsing, correctness lebih penting daripada kecepatan implementasi.

Lebih baik sebuah field belum tersedia daripada menampilkan angka yang tidak terverifikasi.

---

## 25. Related Documentation

- `README.md`
- `AGENTS.md`
- `docs/ARCHITECTURE.md`
- `docs/CONFIGURATION.md`
- `docs/BENCHMARKS.md`
- `docs/SAVE-READER.md`
- `docs/GAME-SUPPORT.md`
- `docs/DISCORD-RPC.md`
- `docs/TROUBLESHOOTING.md`
- `docs/ROADMAP.md`
