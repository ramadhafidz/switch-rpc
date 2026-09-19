# DEVELOPMENT.md

Panduan pengembangan untuk **SWITCH RPC**.

Dokumen ini menjelaskan workflow development, struktur kode, cara menjalankan komponen, testing, debugging, dan aturan praktis ketika menambahkan atau mengubah fitur.

---

## 1. Tujuan Dokumen

Gunakan dokumen ini sebagai panduan ketika:

- menyiapkan environment development;
- menjalankan aplikasi secara lokal;
- mengembangkan detector, save reader, atau Discord RPC;
- mengubah bridge C#;
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

---

## 2. Prasyarat

Environment utama proyek saat ini adalah:

- Windows 11
- Python 3.9+
- Python virtual environment
- .NET SDK yang sesuai dengan project bridge
- Discord Desktop
- Nintendo Switch Eden emulator
- Git

Komponen yang membutuhkan environment Windows:

- Eden process detection;
- window title detection;
- Discord IPC;
- `pywin32`;
- pengujian runtime dengan game berjalan.

---

## 3. Setup Repository

Clone repository:

```powershell
git clone <repository-url>
cd switch-rpc
```

Buat virtual environment:

```powershell
python -m venv .venv
```

Aktifkan:

```powershell
.venv\Scripts\Activate.ps1
```

Install dependency:

```powershell
pip install -r requirements.txt
```

Jika PowerShell memblokir aktivasi script, gunakan interpreter langsung:

```powershell
.venv\Scripts\python.exe -m pip install -r requirements.txt
```

---

## 4. PKHeX Local Setup

Source PKHeX digunakan sebagai dependency lokal untuk bridge save reader.

Struktur yang diharapkan:

```text
bridge/
├── PKHeX/
└── PokemonSaveReader/
```

PKHeX **tidak disimpan sebagai bagian dari repository utama** dan harus tetap di-ignore oleh Git.

Pastikan project `PKHeX.Core` dapat diakses oleh project bridge.

Reference project:

```powershell
dotnet add bridge/PokemonSaveReader/PokemonSaveReader.csproj reference bridge/PKHeX/PKHeX.Core/PKHeX.Core.csproj
```

Build:

```powershell
dotnet build bridge/PokemonSaveReader/PokemonSaveReader.csproj
```

Jika restore terkena source NuGet yang tidak tersedia secara lokal, pendekatan yang digunakan selama development adalah:

```powershell
dotnet restore bridge/PokemonSaveReader/PokemonSaveReader.csproj --ignore-failed-sources
dotnet build bridge/PokemonSaveReader/PokemonSaveReader.csproj --no-restore
```

Jangan mengubah konfigurasi dependency hanya untuk mengatasi error sementara tanpa memahami penyebabnya.

---

## 5. Context7 dan Dokumentasi Dependency

Gunakan **Context7 sebagai sumber utama** ketika membutuhkan dokumentasi library, framework, SDK, atau tool eksternal yang relevan.

Terutama gunakan Context7 ketika:

- membuat integrasi API baru;
- tidak yakin terhadap signature sebuah API;
- library mengalami perubahan versi;
- terjadi error akibat dependency;
- membutuhkan contoh penggunaan;
- bekerja dengan PyPresence, psutil, pywin32, .NET, Discord RPC, atau library eksternal lain.

Prioritas referensi:

1. Context7 untuk dokumentasi versi yang relevan;
2. dokumentasi resmi;
3. source code resmi;
4. dokumentasi versi tertentu;
5. sumber teknis terpercaya lainnya.

Untuk PKHeX, source code lokal adalah referensi penting karena project menggunakan source yang benar-benar di-build secara lokal. Jika dokumentasi eksternal berbeda dengan source version yang digunakan, prioritaskan API yang benar-benar tersedia pada source/version lokal.

**Jangan mengarang API, property, offset save, atau struktur data.**

---

## 6. Menjalankan Aplikasi

Aktifkan virtual environment terlebih dahulu:

```powershell
.venv\Scripts\Activate.ps1
```

Jalankan:

```powershell
python main.py
```

Output normal kira-kira:

```text
SWITCH RPC started.
Connecting to Discord...
Discord RPC connected.
Eden: running
Game detected: Pokémon Legends: Arceus
Rich Presence updated.
```

Aplikasi berjalan sebagai polling loop dan memeriksa status Eden secara berkala.

Interval diatur melalui:

```json
{
	"discord": {
		"update_interval": 15
	}
}
```

---

## 7. Development Workflow

Workflow yang disarankan:

```text
Understand
   ↓
Inspect existing code
   ↓
Check Context7 / official docs
   ↓
Implement smallest change
   ↓
Run focused test
   ↓
Run integration test
   ↓
Review diff
   ↓
Update documentation
   ↓
Commit
```

Jangan langsung melakukan refactor besar ketika masalah dapat diselesaikan dengan perubahan kecil.

### 7.1 Developer CLI

Project menyediakan `dev.py` sebagai entry point development dari repository root.

```powershell
python dev.py lint
python dev.py format
python dev.py typecheck
python dev.py test
python dev.py build
python dev.py check
python dev.py all
```

Command tambahan:

```powershell
python dev.py run
python dev.py save
python dev.py clean
```

Gunakan help untuk melihat command yang tersedia:

```powershell
python dev.py --help
```

CLI ini menjalankan tool yang sama dengan workflow manual, berhenti pada subprocess pertama yang gagal dalam command gabungan, dan meneruskan exit code dari subprocess yang gagal. Jika executable seperti `dotnet` tidak tersedia, CLI menampilkan error yang jelas tanpa menyembunyikan output asli command.

### 7.2 .NET Implementation (src/)

Implementasi .NET 10 native (Phase 2) berada di `src/` dengan solusi `SwitchRpc.slnx`.

Build seluruh solusi:

```powershell
dotnet build SwitchRpc.slnx
```

Jalankan aplikasi .NET:

```powershell
dotnet run --project src/SwitchRpc.App
```

Mode diagnose (tanpa Eden/Discord; mencetak metrik pipeline untuk benchmark):

```powershell
dotnet run --project src/SwitchRpc.App --no-build -- --diagnose
```

Jalankan test .NET:

```powershell
dotnet test SwitchRpc.slnx
```

Detail arsitektur proyek ada di `docs/ARCHITECTURE.md`, dan bukti benchmark migrasi ada di `docs/BENCHMARKS.md`.

---

## 8. Sebelum Mengubah Kode

Sebelum mengubah suatu komponen:

### 8.1 Pahami tanggung jawab file

Contoh:

- `games/detector.py` → mendeteksi Eden dan game;
- `games/registry.py` → lookup konfigurasi game;
- `games/save_paths.py` → menemukan save file;
- `games/save_reader.py` → menjalankan bridge dan membaca JSON;
- `games/game_save_reader.py` → menghubungkan game ID dengan save path;
- `games/state.py` → model state aplikasi;
- `rpc/discord_rpc.py` → komunikasi Discord RPC;
- `bridge/PokemonSaveReader/` → adapter C# menuju PKHeX.Core.

### 8.2 Cari API yang sudah tersedia

Sebelum membuat parser atau implementasi baru:

- cari abstraction yang sudah tersedia;
- periksa source dependency;
- gunakan API yang sudah ada;
- hindari duplikasi logic.

Khusus save file Pokémon, jangan membuat parser manual jika PKHeX sudah menyediakan abstraction yang dibutuhkan.

---

## 9. Python Coding Rules

Python mengikuti style proyek saat ini.

### Indentasi

Gunakan **2 tab** untuk setiap level indentasi.

Contoh:

```python
class Example:
	def run(self):

		if True:
			print("example")
```

Jangan mencampur tab dan spaces untuk indentation.

### General Rules

- Gunakan type hints jika membantu memperjelas API.
- Gunakan `Path` untuk filesystem.
- Hindari hardcoded path milik developer.
- Gunakan exception handling pada boundary eksternal.
- Jangan menelan error secara diam-diam jika error tersebut penting untuk debugging.
- Pertahankan module tetap kecil dan fokus.
- Hindari global mutable state jika tidak diperlukan.

---

## 10. C# Coding Rules

Bridge C# harus tetap sederhana.

Tanggung jawab bridge:

1. menerima path save;
2. memanggil PKHeX.Core;
3. membaca data yang diperlukan;
4. membentuk object hasil;
5. menulis JSON ke stdout;
6. menulis error ke stderr;
7. keluar dengan exit code yang sesuai.

Bridge tidak bertanggung jawab atas:

- Discord RPC;
- game detection;
- polling Eden;
- UI aplikasi;
- konfigurasi Python.

---

## 11. JSON Boundary

Komunikasi Python → C# menggunakan process invocation.

Alur:

```text
Python
  ↓
dotnet PokemonSaveReader.dll <save>
  ↓
PKHeX.Core
  ↓
JSON stdout
  ↓
Python json.loads()
```

Output normal harus berupa JSON yang valid.

Contoh:

```json
{
	"success": true,
	"game": {
		"type": "scarlet_violet"
	},
	"playtime": {
		"hours": 10,
		"minutes": 51,
		"seconds": 28
	}
}
```

Error diagnostic harus menggunakan stderr agar stdout tetap dapat diproses sebagai JSON.

---

## 12. Save Reader Development

Save reader adalah bagian yang paling sensitif terhadap perubahan format game.

Aturan utama:

- read-only;
- jangan menulis kembali save;
- jangan mengubah save;
- jangan menebak offset;
- jangan menebak struktur binary;
- jangan mengandalkan string ASCII yang belum diverifikasi;
- gunakan abstraction PKHeX jika tersedia;
- verifikasi dengan save nyata;
- dokumentasikan hasil verifikasi.

Untuk detail, lihat `docs/SAVE-READER.md`.

---

## 13. Menambahkan Data Save Baru

Jika ingin menambahkan field baru:

### Langkah 1 — Cari abstraction

Cari apakah PKHeX sudah menyediakan:

- property;
- save block;
- accessor;
- helper;
- enum;
- method.

### Langkah 2 — Verifikasi source

Pastikan API tersebut tersedia pada versi PKHeX yang sedang digunakan.

### Langkah 3 — Tambahkan ke bridge

Contoh:

```csharp
return new
{
	success = true,
	playtime = new
	{
		hours = playtime.PlayedHours
	},
	new_data = ...
};
```

### Langkah 4 — Build

```powershell
dotnet build bridge/PokemonSaveReader/PokemonSaveReader.csproj
```

### Langkah 5 — Test langsung

```powershell
dotnet run --project bridge/PokemonSaveReader -- "<save-path>"
```

### Langkah 6 — Test dari Python

Pastikan `SaveReader` dapat membaca JSON tersebut.

---

## 14. Game Detection Development

Detector saat ini menggunakan:

1. process detection;
2. window title Eden;
3. mapping nama game → stable game ID.

Jika menambahkan game baru:

- tambahkan title mapping;
- gunakan stable internal ID;
- tambahkan konfigurasi game;
- jangan menggunakan display name sebagai ID internal;
- verifikasi dengan Eden yang benar-benar menjalankan game tersebut.

Contoh ID:

```text
pokemon_legends_arceus
pokemon_scarlet
pokemon_violet
pokemon_legends_za
```

---

## 15. Save Path Development

Jangan menggunakan path absolut milik satu developer.

Gunakan:

```python
Path.home()
```

untuk menemukan home directory.

Resolver harus:

- menemukan folder save;
- memilih file save yang benar;
- memeriksa keberadaan file;
- mengembalikan `Path | None` ketika save tidak ditemukan.

Jangan mengasumsikan semua game memiliki struktur folder yang identik tanpa verifikasi.

---

## 16. Discord RPC Development

Discord RPC harus tetap menjadi layer terpisah.

`DiscordRPC` bertanggung jawab untuk:

- connect;
- update;
- clear;
- close;
- reconnect setelah connection failure.

Game logic tidak boleh bergantung langsung pada object `Presence` dari PyPresence.

Jika PyPresence berubah:

1. cek dokumentasi versi yang digunakan;
2. cek Context7;
3. cek source/package;
4. ubah wrapper `rpc/discord_rpc.py`;
5. hindari menyebarkan API PyPresence ke seluruh project.

---

## 17. GameState

`GameState` adalah model internal untuk state yang dibutuhkan aplikasi.

Contoh field:

```python
@dataclass
class GameState:
	game_id: str
	playtime_seconds: int | None = None
	pokedex_caught: int | None = None
	pokedex_total: int | None = None
	location: str | None = None
```

Jangan memasukkan detail internal PKHeX secara langsung ke seluruh aplikasi.

Bridge boleh menghasilkan data yang lebih lengkap, tetapi Python sebaiknya mengubahnya menjadi model aplikasi yang stabil.

---

## 18. Testing

Testing dilakukan bertahap.

### 18.1 Unit / focused tests

Jalankan test tertentu ketika mengubah komponen kecil.

Contoh:

```powershell
python test/game_detection.py
```

atau:

```powershell
python test/save_path.py
```

### 18.2 Save Reader Test

Pastikan save nyata dapat dibaca:

```powershell
python test/game_save_reader.py
```

### 18.3 Bridge Test

```powershell
dotnet run --project bridge/PokemonSaveReader -- "<save-path>"
```

### 18.4 Integration Test

Jalankan:

```powershell
python main.py
```

kemudian:

1. buka Discord;
2. jalankan Eden;
3. jalankan salah satu game;
4. pastikan game terdeteksi;
5. pastikan RPC muncul;
6. tutup game;
7. pastikan RPC dibersihkan.

---

## 19. Test Save Tanpa Menulis Save

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

## 20. Debugging

Gunakan debugging bertahap.

### Eden tidak terdeteksi

Periksa:

```powershell
Get-Process eden -ErrorAction SilentlyContinue
```

Periksa window title melalui test yang tersedia.

### Game tidak terdeteksi

Pastikan:

- Eden sedang berjalan;
- game benar-benar aktif;
- window title mengandung nama game yang diharapkan;
- mapping di `GAME_TITLE_MAP` sesuai.

### Discord tidak terhubung

Periksa:

- Discord Desktop sedang berjalan;
- Client/Application ID benar;
- PyPresence terinstall;
- Discord IPC tersedia.

Jangan membuat koneksi RPC baru setiap polling cycle.

### Save tidak ditemukan

Periksa:

- Eden save root;
- title ID;
- folder game;
- file `main`;
- permission;
- apakah game sudah pernah membuat save.

### Bridge gagal

Jalankan bridge secara langsung:

```powershell
dotnet run --project bridge/PokemonSaveReader -- "<save-path>"
```

Pisahkan masalah menjadi:

```text
Path
 ↓
File
 ↓
PKHeX detection
 ↓
Save parsing
 ↓
JSON
 ↓
Python
```

---

## 21. Dependency Changes

Sebelum mengubah dependency:

1. cek versi saat ini;
2. cek dokumentasi;
3. cek breaking changes;
4. cek compatibility;
5. lakukan perubahan kecil;
6. jalankan test;
7. update dokumentasi jika diperlukan.

Jangan upgrade semua dependency sekaligus tanpa alasan.

Setelah perubahan:

```powershell
pip freeze
```

dan/atau:

```powershell
dotnet --info
dotnet --list-sdks
```

Gunakan informasi tersebut untuk membantu reproduksi environment.

---

## 22. Git Workflow

Sebelum commit:

```powershell
git status
```

Review perubahan:

```powershell
git diff
```

Review staged changes:

```powershell
git diff --cached
```

Periksa file yang akan di-commit:

```powershell
git diff --cached --name-only
```

Jangan commit:

- `.venv/`;
- save file;
- emulator data;
- `bridge/PKHeX/`;
- build output;
- secret;
- local configuration;
- cache.

Gunakan `.gitignore` sebagai perlindungan tambahan, tetapi tetap review `git status`.

---

## 23. Commit

Gunakan commit yang menjelaskan perubahan.

Contoh:

```text
feat(save-reader): support PLA and Scarlet/Violet saves
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

## 24. Dokumentasi

Setiap fitur besar harus diperbarui dokumentasinya.

Contoh:

| Perubahan | Dokumentasi |
|---|---|
| Architecture | `docs/ARCHITECTURE.md` |
| Development workflow | `docs/DEVELOPMENT.md` |
| Config | `docs/CONFIGURATION.md` |
| Save reader | `docs/SAVE-READER.md` |
| Game support | `docs/GAME-SUPPORT.md` |
| Discord RPC | `docs/DISCORD-RPC.md` |
| Troubleshooting | `docs/TROUBLESHOOTING.md` |
| Roadmap | `docs/ROADMAP.md` |

Jika behavior berubah, README juga perlu diperiksa.

---

## 25. Menambahkan Game Baru

Workflow:

```text
1. Tentukan stable game ID
        ↓
2. Tambahkan detector mapping
        ↓
3. Tambahkan config
        ↓
4. Verifikasi title ID
        ↓
5. Verifikasi save location
        ↓
6. Verifikasi save format
        ↓
7. Implementasi save reader
        ↓
8. Tambahkan GameState mapping
        ↓
9. Test save
        ↓
10. Test Eden
        ↓
11. Test Discord RPC
        ↓
12. Update documentation
```

Jangan menyatakan game "supported" hanya karena detector sudah mengenal nama game.

Detection support dan save-reader support adalah dua hal berbeda.

---

## 26. Performance

Polling harus tetap ringan.

Prinsip:

- jangan membaca save setiap detik;
- jangan membuat process baru tanpa kebutuhan;
- jangan reconnect Discord secara terus-menerus;
- gunakan interval konfigurasi;
- hindari scan filesystem berlebihan;
- cache informasi yang stabil jika diperlukan.

Jika save reader membutuhkan waktu cukup lama, jangan menjalankannya lebih sering daripada yang diperlukan untuk memperbarui Rich Presence.

---

## 27. Privacy dan Security

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

## 28. Checklist Sebelum Pull Request

### Code

- [ ] Perubahan sesuai tanggung jawab module.
- [ ] Tidak ada hardcoded user path.
- [ ] Tidak ada API yang diada-adakan.
- [ ] Python menggunakan 2-tab indentation.
- [ ] Error handling cukup.
- [ ] Tidak ada save modification.

### Save Reader

- [ ] Format save telah diverifikasi.
- [ ] API PKHeX telah diverifikasi.
- [ ] Tidak ada guessed offset.
- [ ] Bridge menghasilkan JSON valid.
- [ ] Save tetap read-only.

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

## 29. Prinsip Development

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

## 30. Related Documentation

- `README.md`
- `AGENTS.md`
- `docs/ARCHITECTURE.md`
- `docs/CONFIGURATION.md`
- `docs/SAVE-READER.md`
- `docs/GAME-SUPPORT.md`
- `docs/DISCORD-RPC.md`
- `docs/TROUBLESHOOTING.md`
- `docs/ROADMAP.md`
