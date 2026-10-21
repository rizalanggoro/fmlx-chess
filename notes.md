# Catatan: Implementasi vs Class Diagram

Dokumen ini mencatat bagian yang **sengaja tidak dipakai** atau **ditambahkan** sehingga
implementasi berbeda dari `class-diagram.md` / `class-diagram.mmd`.

Sumber diagram: gist `DeltDev` (lihat header `class-diagram.md`). Kedua file diagram bersifat
read-only dan tidak diubah; semua penyimpangan dicatat di sini.

## 1. Ada di diagram, tidak dipakai

| Item | Kondisi di kode | Alasan |
| --- | --- | --- |
| `PieceMovedEventArgs` + event `PieceMoved` | Dikomentari di `Services/GameManager.cs` dan `Interfaces/Services/IGameManager.cs`; file `Events/PieceMovedEventArgs.cs` dibiarkan ada | Papan dikirim sebagai snapshot lewat `BoardChanged`, bukan per langkah. Event langkah hanya membawa `from`/`to`, sehingga kasus castling, en passant, dan promosi tidak cukup untuk menyusun ulang papan di klien |
| `SquareClickedEventArgs` | File `Events/SquareClickedEventArgs.cs` ada, tidak pernah dirujuk | Interaksi kotak papan ditangani di frontend, tidak ada event klik di engine |

## 2. Disesuaikan dari diagram

| Diagram | Implementasi |
| --- | --- |
| `Board` menyimpan `CastlingRights` dan en passant target | `Board` hanya menyimpan `Tiles`; `CastlingRights` dan `EnPassantTarget` disimpan di `GameManager` |
| Nama event `PieceMoved`, `PieceCaptured`, `TurnChanged`, `CheckDeclared`, `GameEnded`, `DrawOffered`, `TimerTick` | Memakai sufiks konvensi .NET: `PieceMovedEventHandler`, `PieceCapturedEventHandler`, `TurnChangedEventHandler`, `CheckDeclaredEventHandler`, `GameEndedEventHandler`, `DrawOfferedEventHandler`, `TimerTickedEventHandler` |
| Tidak ada `DrawDeclined` | Ada `Events/DrawDeclinedEventArgs.cs` + `DrawDeclinedEventHandler` (jawaban penolakan draw) |

## 3. Ditambahkan di luar diagram

Domain / engine:

- `Events/BoardChangedEventArgs.cs` + `BoardChangedEventHandler`, dipicu di `TryMakeMove` dan `SetupInitialBoardPosition` (start/reset). Membawa `IBoard` sebagai referensi; pelanggan langsung memetakannya menjadi 64 `TileDto`.
- `Interfaces/Services/IGameManager.cs` sebagai kontrak `GameManager`.
- Helper privat tambahan di `GameManager`: `AddPawnMove`, `ResetTimers`, `OnTimeExpired`, `Opposite` (tidak ada di diagram, hanya pemecahan implementasi).

Di luar domain (tidak digambarkan sama sekali di class diagram):

- `Controllers/`: `RoomsController` (`/rooms`), `GameController` (`/rooms/{roomId}/game`).
- `Services/RoomRegistry.cs` + `Interfaces/Services/IRoomRegistry.cs`: registry room in-memory, memegang satu `IGameManager` per room, dan menjembatani event engine ke SignalR.
- `Models/GameRoom.cs` (room + mode + status kursi), `Enums/RoomMode.cs` (`HotSeat`, `Online`).
- `Hubs/GameHub.cs`, `Hubs/GameGroups.cs`: grup `lobby` dan `room:{id}`.
- `DTOs/`: kontrak HTTP (`RoomStatusDto`, `GameStateDto`, `TileDto`, `PlayerDto`, `RoomSessionDto`, `ApiErrorDto`), `DTOs/Requests/`, dan `DTOs/Events/` untuk payload SignalR (`BoardChangedDto`, `LobbyChangedDto`, `PieceCapturedDto`, `CheckDeclaredDto`, `DrawOfferedDto`, `DrawDeclinedDto`, `TimerTickDto`, `GameEndedDto`).
- `Swagger/`: `RequiredSchemaFilter`, `SignalRContractSchemaFilter` (mendaftarkan DTO event agar ikut codegen FE).
- `GameStateDto` menambahkan data yang tidak ada di domain diagram: sisa waktu kedua pemain, `moveCount`, castling rights, en passant target.

## 4. Peta event engine ke siaran SignalR

| Event engine | Dipakai | Dikirim ke klien sebagai |
| --- | --- | --- |
| `BoardChangedEventHandler` | ya | `BoardChanged` (snapshot 64 tile) |
| `PieceMovedEventHandler` | tidak (dikomentari) | tidak dikirim |
| `PieceCapturedEventHandler` | ya | `PieceCaptured` |
| `TurnChangedEventHandler` | ya | `GameChanged` (tanpa payload, klien refetch) |
| `CheckDeclaredEventHandler` | ya | `CheckDeclared` |
| `GameEndedEventHandler` | ya | `GameEnded` |
| `DrawOfferedEventHandler` | ya | `DrawOffered` |
| `DrawDeclinedEventHandler` | ya | `DrawDeclined` |
| `TimerTickedEventHandler` | ya | `TimerTick` |
