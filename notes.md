# Catatan: Implementasi vs Class Diagram

Dokumen ini mencatat bagian yang **sengaja tidak dipakai** atau **ditambahkan** sehingga
implementasi berbeda dari `class-diagram.md` / `class-diagram.mmd`.

Sumber diagram: gist `DeltDev` (lihat header `class-diagram.md`). Kedua file diagram bersifat
read-only dan tidak diubah; semua penyimpangan dicatat di sini.

## 1. Ada di diagram, tidak dipakai

| Item | Kondisi di kode | Alasan |
| --- | --- | --- |
| `PieceMovedEventArgs` + event `PieceMoved` | Dikomentari di `Services/GameManager.cs` dan `Interfaces/Services/IGameManager.cs`; file `Events/PieceMovedEventArgs.cs` dibiarkan ada | State room dikirim sebagai snapshot lewat `RoomSnapshotChanged`, bukan per langkah. Event langkah hanya membawa `from`/`to`, sehingga kasus castling, en passant, dan promosi tidak cukup untuk menyusun ulang papan di klien |
| `SquareClickedEventArgs` | File `Events/SquareClickedEventArgs.cs` ada; deklarasi event `SquareClicked` hanya dikomentari di `Services/GameManager.cs` dan `Interfaces/Services/IGameManager.cs` (tidak ada di diagram, sekadar placeholder) | Interaksi kotak papan ditangani di frontend, tidak ada event klik di engine |
| `GetPieces(PieceColor)` | Ada di `Services/GameManager.cs` dengan `[Obsolete]` + summary; tidak ada pemanggil | Daftar bidak per warna tidak pernah dibutuhkan; posisi bidak sudah dibaca langsung dari `GameBoard` dan dikirim sebagai `board` di `SnapshotChangedDto` |
| `PromotionSelector` + delegate `PromotionSelectionHandler` | Dikomentari di `Services/GameManager.cs` dan `Models/PromotionSelectionHandler.cs`; file delegate dibiarkan ada | Promosi memakai `PromotionType` yang sudah dibawa langkah legal (Queen/Rook/Bishop/Knight). Request tanpa `promotionType` memilih langkah pertama (Queen), jadi hook selector tidak pernah terpanggil |
| `PlayerTimer` + `IPlayerTimer` | Keduanya tetap ada, tetapi hanya sebagai model data: satu properti `RemainingTime` (`get; set;`), tanpa `StartTimer`/`StopTimer`/event. Countdown, tick, dan timeout dipindah ke `Services/GameManager.cs` (`StartCountdown`, `StopCountdown`, `StopAllCountdowns`, `TickCountdown`, `OnTimeExpired`) | Class diagram salah: logic timer seharusnya berada di `GameManager`; `PlayerTimer` cukup menyimpan sisa waktu. Catatan juga ditulis sebagai summary di `PlayerTimeTracker` dan `ResetTimers` |

## 2. Disesuaikan dari diagram

| Diagram | Implementasi |
| --- | --- |
| `Board` menyimpan `CastlingRights` dan en passant target | `Board` hanya menyimpan `Tiles`; `CastlingRights` dan `EnPassantTarget` disimpan di `GameManager` |
| Nama event `PieceMoved`, `PieceCaptured`, `TurnChanged`, `CheckDeclared`, `GameEnded`, `DrawOffered`, `TimerTick` | Memakai sufiks konvensi .NET: `PieceMovedEventHandler`, `PieceCapturedEventHandler`, `TurnChangedEventHandler`, `CheckDeclaredEventHandler`, `GameEndedEventHandler`, `DrawOfferedEventHandler`, `TimerTickedEventHandler` |
| Tidak ada `DrawDeclined` | Ada `Events/DrawDeclinedEventArgs.cs` + `DrawDeclinedEventHandler` (jawaban penolakan draw) |
| `SetupInitialBoardPosition()` publik | Dibuat `private` di `Services/GameManager.cs`; hanya dipanggil dari `StartGame` dan `ResetGame`, tidak dipakai controller atau service lain |
| `IsFiftyMoveRuleClaimAvailable()` publik | Dibuat `private` di `Services/GameManager.cs`; hanya dipanggil dari `ClaimDrawByFiftyMoveRule`, tidak dipakai controller atau service lain |
| `IsSeventyFiveMoveRuleAutomatic()` publik | Dibuat `private` di `Services/GameManager.cs`; hanya dipanggil dari `EvaluateAutomaticResult`, tidak dipakai controller atau service lain |
| `GeneratePositionKey()` publik | Dibuat `private` di `Services/GameManager.cs`; hanya dipanggil dari `GameManager` sendiri untuk mencatat dan menghitung pengulangan posisi |
| `ApplyMove(IBoard, Move)` | Mengembalikan `MoveApplicationResult?`: `null` kalau petak asal kosong dan papan tidak diubah, jadi caller yang memutuskan; tipe parameter tetap sesuai diagram |
| `Reset()` publik | Dibuat `private` di `Services/GameManager.cs`; hanya clearing riwayat posisi dan half-move clock, dipanggil dari `StartGame` dan `ResetGame`, tidak dipakai controller atau service lain |

## 3. Ditambahkan di luar diagram

Domain / engine:

- `Events/SnapshotChangedEventArgs.cs` + `SnapshotChangedEventHandler`, dipicu di `TryMakeMove` (setelah hasil otomatis dievaluasi dan giliran berganti), `StartGame`, dan `ResetGame`. Membawa `IBoard` dan riwayat langkah sebagai referensi.
- `Interfaces/Services/IGameManager.cs` sebagai kontrak `GameManager`.
- Helper privat tambahan di `GameManager`: `AddPawnMove`, `ResetTimers`, `OnTimeExpired` (tidak ada di diagram, hanya pemecahan implementasi).
- `ValueObjects/SquareOffset.cs` + `Constants/SquareOffsets.cs`: tipe offset (`Column`/`Row` sebagai delta, plus operator `Position + SquareOffset`) dan daftar offset bersama — knight, diagonal, lurus, raja, ratu, dan kolom tangkap pion — dipakai ulang oleh `GenerateKnightMoves`, `GenerateSlidingMoves`, `GenerateKingMoves`, `GeneratePawnMoves`, dan `IsSquareAttacked`.

Di luar domain (tidak digambarkan sama sekali di class diagram):

- `Controllers/`: `RoomsController` (`/rooms`), `GameController` (`/rooms/{roomId}/game`).
- `Services/RoomRegistry.cs` + `Interfaces/Services/IRoomRegistry.cs`: registry room in-memory, memegang satu `IGameManager` per room, dan menjembatani event engine ke SignalR.
- `Models/GameRoom.cs` (room + mode + status kursi), `Enums/RoomMode.cs` (`HotSeat`, `Online`).
- `Hubs/GameHub.cs`, `Hubs/GameGroups.cs`: grup `lobby` dan `room:{id}`.
- `DTOs/`: kontrak HTTP (`RoomStatusDto`, `GameStateDto`, `TileDto`, `PlayerDto`, `RoomSessionDto`, `ApiErrorDto`), `DTOs/Requests/`, dan `DTOs/Events/` untuk payload SignalR (`RoomSnapshotChangedDto`, `LobbyChangedDto`, `PieceCapturedDto`, `CheckDeclaredDto`, `DrawOfferedDto`, `DrawDeclinedDto`, `TimerTickDto`, `GameEndedDto`). `RoomSnapshotChangedDto` membawa `room`, `state`, 64 `board`, dan `history` sekaligus.
- `Swagger/`: `RequiredSchemaFilter`, `SignalRContractSchemaFilter` (mendaftarkan DTO event agar ikut codegen FE).
- `GameStateDto` menambahkan data yang tidak ada di domain diagram: sisa waktu kedua pemain, `moveCount`, castling rights, en passant target.

## 4. Peta event engine ke siaran SignalR

| Event engine | Dipakai | Dikirim ke klien sebagai |
| --- | --- | --- |
| `SnapshotChangedEventHandler` | ya | `RoomSnapshotChanged` (room + state + 64 tile + riwayat) |
| `PieceMovedEventHandler` | tidak (dikomentari) | tidak dikirim |
| `PieceCapturedEventHandler` | ya | `PieceCaptured` |
| `TurnChangedEventHandler` | tidak di-hook | tidak dikirim (turn ikut di snapshot) |
| `CheckDeclaredEventHandler` | ya | `CheckDeclared` |
| `GameEndedEventHandler` | ya | `GameEnded` |
| `DrawOfferedEventHandler` | ya | `DrawOffered` |
| `DrawDeclinedEventHandler` | ya | `DrawDeclined` |
| `TimerTickedEventHandler` | ya | `TimerTick` |

Event transport lain: `LobbyChanged` (grup `lobby`, dipicu `NotifyLobby`), tidak berasal dari event engine.

Event transport yang dihapus dari versi sebelumnya: `GameChanged`, `BoardChanged`, `HistoryChanged` (semuanya digantikan `RoomSnapshotChanged`).
