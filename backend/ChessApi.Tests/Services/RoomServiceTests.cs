using Api.DTOs;
using Api.Enums;
using Api.GameEngine;
using Api.Hubs;
using Api.Interfaces.GameEngine;
using Api.Interfaces.Models;
using Api.Models;
using Api.Services;
using Api.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ChessApi.Tests.Services;

[TestFixture]
public class RoomServiceTests
{
    private Mock<IHubContext<GameHub>> _hubContextMock = null!;
    private Mock<IHubClients> _hubClientsMock = null!;
    private Mock<IClientProxy> _clientProxyMock = null!;
    private RoomRegistry _registry = null!;
    private ILogger<RoomService> _logger = null!;
    private IBoardInitializer _boardInitializer = null!;
    private RoomService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _hubContextMock = new Mock<IHubContext<GameHub>>();
        _hubClientsMock = new Mock<IHubClients>();
        _clientProxyMock = new Mock<IClientProxy>();

        _hubClientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);
        _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);

        _registry = new RoomRegistry();
        _logger = NullLogger<RoomService>.Instance;
        _boardInitializer = new StandardBoardInitializer();

        _service = new RoomService(_hubContextMock.Object, _registry, _logger, _boardInitializer);
    }

    private class CustomTestBoardInitializer(Action<IBoard> configure) : IBoardInitializer
    {
        public void Initialize(IBoard board)
        {
            for (var r = 0; r < 8; r++)
            for (var c = 0; c < 8; c++)
                board.Tiles[r, c].Piece = null;

            configure(board);
        }
    }

    private Room CreateTestRoom(string roomId, RoomMode mode, Action<Room>? configure = null, IBoardInitializer? initializer = null)
    {
        var engine = new ChessGameEngine(
            new Board(),
            new Player("White", PieceColor.White),
            new Player("Black", PieceColor.Black),
            TimeSpan.FromMinutes(10),
            initializer ?? _boardInitializer
        );
        var room = new Room(roomId, mode, engine);
        configure?.Invoke(room);
        _registry.Create(room);
        return room;
    }

    #region CreateRoom & Listing

    [Test]
    public async Task CreateRoom_HotSeatMode_StartsGameAndReturnsSessionWithNullColor()
    {
        var request = new CreateRoomDto(RoomMode.HotSeat, null, null);

        var result = await _service.CreateRoom(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data!.Mode, Is.EqualTo(RoomMode.HotSeat));
            Assert.That(result.Data.Color, Is.Null);

            var createdRoom = _registry.Get(result.Data.Id);
            Assert.That(createdRoom, Is.Not.Null);
            Assert.That(createdRoom!.Engine.State, Is.EqualTo(GameState.InProgress));
        });
    }

    [Test]
    public async Task CreateRoom_OnlineMode_SetsCreatorSeatAndNotifiesLobby()
    {
        var request = new CreateRoomDto(RoomMode.Online, PieceColor.Black, "Alice");

        var result = await _service.CreateRoom(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data!.Mode, Is.EqualTo(RoomMode.Online));
            Assert.That(result.Data.Color, Is.EqualTo(PieceColor.Black));

            var createdRoom = _registry.Get(result.Data.Id);
            Assert.That(createdRoom, Is.Not.Null);
            Assert.That(createdRoom!.BlackJoined, Is.True);
            Assert.That(createdRoom.BlackName, Is.EqualTo("Alice"));
            Assert.That(createdRoom.WhiteJoined, Is.False);
        });

        _clientProxyMock.Verify(c => c.SendCoreAsync("LobbyChanged", It.IsAny<object[]>(), default), Times.Once);
    }

    [Test]
    public async Task CreateRoom_WithLongPlayerName_NormalizesAndTruncatesName()
    {
        const string longName = "   ThisIsAVeryLongNameExceeding24Chars   ";
        var request = new CreateRoomDto(RoomMode.Online, PieceColor.White, longName);

        var result = await _service.CreateRoom(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            var room = _registry.Get(result.Data!.Id);
            Assert.That(room, Is.Not.Null);
            Assert.That(room!.WhiteName, Is.EqualTo("ThisIsAVeryLongNameExcee"));
            Assert.That(room.WhiteName!.Length, Is.EqualTo(24));
        });
    }

    [Test]
    public void AvailableRooms_WithMixedRooms_ReturnsOnlyOnlineRoomsWithOneSeatAvailable()
    {
        // HotSeat room (both seats occupied by default)
        CreateTestRoom("HOT001", RoomMode.HotSeat);

        // Online room with 1 seat taken
        CreateTestRoom("ON001", RoomMode.Online, r =>
        {
            r.WhiteJoined = true;
            r.WhiteName = "Alice";
        });

        // Online room with both seats taken
        CreateTestRoom("ON002", RoomMode.Online, r =>
        {
            r.WhiteJoined = true;
            r.BlackJoined = true;
        });

        var available = _service.AvailableRooms();

        Assert.Multiple(() =>
        {
            Assert.That(available, Has.Count.EqualTo(1));
            Assert.That(available[0].Id, Is.EqualTo("ON001"));
        });
    }

    #endregion

    #region JoinRoom

    [Test]
    public async Task JoinRoom_RoomDoesNotExist_Returns404NotFound()
    {
        var dto = new JoinRoomDto(PieceColor.White, "Alice");

        var result = await _service.JoinRoom("UNKNOWN", dto);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
            Assert.That(result.ErrorMessage, Is.EqualTo("Room not found"));
        });
    }

    [Test]
    public async Task JoinRoom_HotSeatRoom_Returns400BadRequest()
    {
        CreateTestRoom("HOT001", RoomMode.HotSeat);
        var dto = new JoinRoomDto(PieceColor.White, "Alice");

        var result = await _service.JoinRoom("HOT001", dto);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(result.ErrorMessage, Is.EqualTo("Only online rooms can be joined"));
        });
    }

    [Test]
    public async Task JoinRoom_MissingColor_Returns400BadRequest()
    {
        CreateTestRoom("ON001", RoomMode.Online);
        var dto = new JoinRoomDto(null, "Alice");

        var result = await _service.JoinRoom("ON001", dto);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(result.ErrorMessage, Is.EqualTo("Color is required"));
        });
    }

    [Test]
    public async Task JoinRoom_SeatAlreadyTaken_Returns409Conflict()
    {
        CreateTestRoom("ON001", RoomMode.Online, r =>
        {
            r.WhiteJoined = true;
            r.WhiteName = "Alice";
        });
        var dto = new JoinRoomDto(PieceColor.White, "Bob");

        var result = await _service.JoinRoom("ON001", dto);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
            Assert.That(result.ErrorMessage, Is.EqualTo("Seat is already taken"));
        });
    }

    [Test]
    public async Task JoinRoom_BothSeatsAlreadyTaken_Returns409RoomIsFull()
    {
        CreateTestRoom("ON001", RoomMode.Online, r =>
        {
            r.WhiteJoined = true;
            r.BlackJoined = true;
        });
        var dto = new JoinRoomDto(PieceColor.White, "Bob");

        var result = await _service.JoinRoom("ON001", dto);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
            Assert.That(result.ErrorMessage, Is.EqualTo("Room is full"));
        });
    }

    [Test]
    public async Task JoinRoom_ValidSeat_ClaimsSeatStartsGameAndNotifiesLobby()
    {
        var room = CreateTestRoom("ON001", RoomMode.Online, r =>
        {
            r.WhiteJoined = true;
            r.WhiteName = "Alice";
        });
        var dto = new JoinRoomDto(PieceColor.Black, "Bob");

        var result = await _service.JoinRoom("ON001", dto);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data!.Color, Is.EqualTo(PieceColor.Black));
            Assert.That(room.BlackJoined, Is.True);
            Assert.That(room.BlackName, Is.EqualTo("Bob"));
            Assert.That(room.Engine.State, Is.EqualTo(GameState.InProgress));
        });

        _clientProxyMock.Verify(c => c.SendCoreAsync("LobbyChanged", It.IsAny<object[]>(), default), Times.Once);
    }

    #endregion

    #region Game Actions

    [Test]
    public void StartGame_RoomDoesNotExist_Returns404NotFound()
    {
        var result = _service.StartGame("UNKNOWN");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        });
    }

    [Test]
    public void StartGame_RoomExists_StartsGameAndReturnsState()
    {
        var room = CreateTestRoom("ON001", RoomMode.Online);

        var result = _service.StartGame("ON001");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(room.Engine.State, Is.EqualTo(GameState.InProgress));
        });
    }

    [Test]
    public void ResetGame_RoomDoesNotExist_Returns404NotFound()
    {
        var result = _service.ResetGame("UNKNOWN");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        });
    }

    [Test]
    public void ResetGame_RoomExists_ResetsGameAndReturnsState()
    {
        var room = CreateTestRoom("HOT001", RoomMode.HotSeat);
        room.Engine.StartGame();

        var result = _service.ResetGame("HOT001");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(room.Engine.State, Is.EqualTo(GameState.NotStarted));
        });
    }

    [Test]
    public void MakeMove_RoomDoesNotExist_Returns404NotFound()
    {
        var dto = new MakeMoveDto(new Position(1, 4), new Position(3, 4));

        var result = _service.MakeMove("UNKNOWN", dto);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        });
    }

    [Test]
    public void MakeMove_OnlineModeAndPlayerNotJoined_Returns403Forbidden()
    {
        CreateTestRoom("ON001", RoomMode.Online, r =>
        {
            r.WhiteJoined = true;
            r.Engine.StartGame();
        });
        // Black has not joined
        var dto = new MakeMoveDto(new Position(1, 4), new Position(3, 4), Color: PieceColor.Black);

        var result = _service.MakeMove("ON001", dto);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
            Assert.That(result.ErrorMessage, Is.EqualTo("You are not a player in this room"));
        });
    }

    [Test]
    public void MakeMove_OnlineModeAndNotPlayersTurn_Returns403Forbidden()
    {
        CreateTestRoom("ON001", RoomMode.Online, r =>
        {
            r.WhiteJoined = true;
            r.BlackJoined = true;
            r.Engine.StartGame(); // White's turn initially
        });
        // Black tries to move on White's turn
        var dto = new MakeMoveDto(new Position(6, 4), new Position(4, 4), Color: PieceColor.Black);

        var result = _service.MakeMove("ON001", dto);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
            Assert.That(result.ErrorMessage, Is.EqualTo("Not your turn"));
        });
    }

    [Test]
    public void MakeMove_IllegalMove_Returns400BadRequest()
    {
        CreateTestRoom("HOT001", RoomMode.HotSeat, r => r.Engine.StartGame());
        // Illegal move: moving White pawn backwards
        var dto = new MakeMoveDto(new Position(1, 4), new Position(0, 4));

        var result = _service.MakeMove("HOT001", dto);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(result.ErrorMessage, Is.EqualTo("Illegal move"));
        });
    }

    [Test]
    public void MakeMove_ValidMove_UpdatesBoardAndReturnsSuccess()
    {
        CreateTestRoom("HOT001", RoomMode.HotSeat, r => r.Engine.StartGame());
        var dto = new MakeMoveDto(new Position(1, 4), new Position(3, 4));

        var result = _service.MakeMove("HOT001", dto);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data!.CurrentPlayer.Color, Is.EqualTo(PieceColor.Black));
        });
    }

    [Test]
    public void Resign_RoomDoesNotExist_Returns404NotFound()
    {
        var action = new PlayerActionDto(PieceColor.White, "Alice");

        var result = _service.Resign("UNKNOWN", action);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        });
    }

    [Test]
    public void Resign_OnlineModeAndNotAPlayer_Returns403Forbidden()
    {
        CreateTestRoom("ON001", RoomMode.Online, r =>
        {
            r.WhiteJoined = true;
            r.Engine.StartGame();
        });
        // Black has not joined
        var action = new PlayerActionDto(PieceColor.Black, "Intruder");

        var result = _service.Resign("ON001", action);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        });
    }

    [Test]
    public void Resign_ValidPlayer_ResignsGameAndReturnsSuccess()
    {
        var room = CreateTestRoom("HOT001", RoomMode.HotSeat, r => r.Engine.StartGame());
        var action = new PlayerActionDto(PieceColor.White, "Alice");

        var result = _service.Resign("HOT001", action);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(room.Engine.State, Is.EqualTo(GameState.Completed));
            Assert.That(room.Engine.Result, Is.Not.Null);
            Assert.That(room.Engine.Result!.Winner, Is.EqualTo(PieceColor.Black));
        });
    }

    [Test]
    public void Resign_OnlineModeAndBlackResigns_EndsGameWithWhiteWin()
    {
        var room = CreateTestRoom("ON001", RoomMode.Online, r =>
        {
            r.WhiteJoined = true;
            r.WhiteName = "Alice";
            r.BlackJoined = true;
            r.BlackName = "Bob";
            r.Engine.StartGame();
        });

        var action = new PlayerActionDto(PieceColor.Black, "Bob");
        var result = _service.Resign("ON001", action);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(room.Engine.State, Is.EqualTo(GameState.Completed));
            Assert.That(room.Engine.Result, Is.Not.Null);
            Assert.That(room.Engine.Result!.Winner, Is.EqualTo(PieceColor.White));
            Assert.That(room.Engine.Result.Reason, Is.EqualTo(GameEndReason.PlayerResigned));
        });
    }

    [Test]
    public void OfferDraw_RoomDoesNotExist_Returns404NotFound()
    {
        var action = new PlayerActionDto(PieceColor.White, "Alice");

        var result = _service.OfferDraw("UNKNOWN", action);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        });
    }

    [Test]
    public void OfferDraw_ValidPlayer_OffersDrawAndReturnsSuccess()
    {
        CreateTestRoom("HOT001", RoomMode.HotSeat, r => r.Engine.StartGame());
        var action = new PlayerActionDto(PieceColor.White, "Alice");

        var result = _service.OfferDraw("HOT001", action);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public void RespondToDrawOffer_RoomDoesNotExist_Returns404NotFound()
    {
        var action = new PlayerActionDto(PieceColor.Black, "Bob", Accepted: true);

        var result = _service.RespondToDrawOffer("UNKNOWN", action);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        });
    }

    [Test]
    public void RespondToDrawOffer_WhenOfferAccepted_EndsGameInDraw()
    {
        var room = CreateTestRoom("HOT001", RoomMode.HotSeat, r =>
        {
            r.Engine.StartGame();
            r.Engine.OfferDraw(new Player("White", PieceColor.White));
        });

        var action = new PlayerActionDto(PieceColor.Black, "Bob", Accepted: true);
        var result = _service.RespondToDrawOffer("HOT001", action);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(room.Engine.State, Is.EqualTo(GameState.Completed));
            Assert.That(room.Engine.Result, Is.Not.Null);
            Assert.That(room.Engine.Result!.Reason, Is.EqualTo(GameEndReason.DrawByAgreement));
        });
    }

    [Test]
    public void RespondToDrawOffer_WhenOfferDeclined_RetainsInProgressState()
    {
        var room = CreateTestRoom("HOT001", RoomMode.HotSeat, r =>
        {
            r.Engine.StartGame();
            r.Engine.OfferDraw(new Player("White", PieceColor.White));
        });

        var action = new PlayerActionDto(PieceColor.Black, "Bob", Accepted: false);
        var result = _service.RespondToDrawOffer("HOT001", action);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(room.Engine.State, Is.EqualTo(GameState.InProgress));
            Assert.That(room.Engine.Result, Is.Null);
        });
    }

    [Test]
    public void ClaimThreefoldRepetition_ConditionNotMet_Returns400BadRequest()
    {
        CreateTestRoom("HOT001", RoomMode.HotSeat, r => r.Engine.StartGame());
        var action = new PlayerActionDto(PieceColor.White, "White");

        var result = _service.ClaimThreefoldRepetition("HOT001", action);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(result.ErrorMessage, Is.EqualTo("Threefold repetition claim not available"));
        });
    }

    [Test]
    public void ClaimThreefoldRepetition_ConditionMet_Returns200OkAndEndsGame()
    {
        var room = CreateTestRoom("HOT001", RoomMode.HotSeat, r =>
        {
            r.Engine.StartGame();
            // 4 cycles = position repeated
            for (var i = 0; i < 2; i++)
            {
                r.Engine.TryMakeMove(new Move(new Position(0, 6), new Position(2, 5)));
                r.Engine.TryMakeMove(new Move(new Position(7, 6), new Position(5, 5)));
                r.Engine.TryMakeMove(new Move(new Position(2, 5), new Position(0, 6)));
                r.Engine.TryMakeMove(new Move(new Position(5, 5), new Position(7, 6)));
            }
        });

        var action = new PlayerActionDto(PieceColor.White, "White");
        var result = _service.ClaimThreefoldRepetition("HOT001", action);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data!.State, Is.EqualTo(GameState.Completed));
            Assert.That(result.Data.Result?.Reason, Is.EqualTo(GameEndReason.DrawByThreefoldRepetition));
            Assert.That(room.Engine.State, Is.EqualTo(GameState.Completed));
        });
    }

    [Test]
    public void ClaimFiftyMoveRule_ConditionNotMet_Returns400BadRequest()
    {
        CreateTestRoom("HOT001", RoomMode.HotSeat, r => r.Engine.StartGame());
        var action = new PlayerActionDto(PieceColor.White, "White");

        var result = _service.ClaimFiftyMoveRule("HOT001", action);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(result.ErrorMessage, Is.EqualTo("Fifty-move claim not available"));
        });
    }

    [Test]
    public void ClaimFiftyMoveRule_ConditionMet_Returns200OkAndEndsGame()
    {
        var initializer = new CustomTestBoardInitializer(board =>
        {
            board.Tiles[0, 4].Piece = new ChessPiece(PieceColor.White, PieceType.King);
            board.Tiles[0, 1].Piece = new ChessPiece(PieceColor.White, PieceType.Knight);
            board.Tiles[1, 7].Piece = new ChessPiece(PieceColor.White, PieceType.Pawn);
            board.Tiles[7, 7].Piece = new ChessPiece(PieceColor.Black, PieceType.King);
            board.Tiles[6, 0].Piece = new ChessPiece(PieceColor.Black, PieceType.Pawn);
        });

        var room = CreateTestRoom("HOT001", RoomMode.HotSeat, r =>
        {
            r.Engine.StartGame();

            var whiteKnightMoves = new[]
            {
                new Move(new Position(0, 1), new Position(2, 2)),
                new Move(new Position(2, 2), new Position(4, 3)),
                new Move(new Position(4, 3), new Position(6, 2)),
                new Move(new Position(6, 2), new Position(4, 1)),
                new Move(new Position(4, 1), new Position(2, 0)),
                new Move(new Position(2, 0), new Position(0, 1)),
            };

            var blackKingMoves = new[]
            {
                new Move(new Position(7, 7), new Position(7, 6)),
                new Move(new Position(7, 6), new Position(6, 5)),
                new Move(new Position(6, 5), new Position(5, 6)),
                new Move(new Position(5, 6), new Position(6, 7)),
                new Move(new Position(6, 7), new Position(7, 7)),
            };

            var w = 0;
            var b = 0;
            for (var i = 0; i < 100; i++)
            {
                var move = i % 2 == 0 ? whiteKnightMoves[w++ % 6] : blackKingMoves[b++ % 5];
                r.Engine.TryMakeMove(move);
            }
        }, initializer);

        var action = new PlayerActionDto(PieceColor.White, "White");
        var result = _service.ClaimFiftyMoveRule("HOT001", action);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data!.State, Is.EqualTo(GameState.Completed));
            Assert.That(result.Data.Result?.Reason, Is.EqualTo(GameEndReason.DrawBy50MoveRule));
        });
    }

    [Test]
    public async Task MakeMove_WhenCreatingRoomThroughService_BroadcastsSignalREvents()
    {
        // 1. Create a room through service (wires up event handlers to SignalR)
        var createResult = await _service.CreateRoom(new CreateRoomDto(RoomMode.HotSeat, PieceColor.White, "Alice"));
        var roomId = createResult.Data!.Id;

        // 2. Play 1. e4 (from 1,4 to 3,4)
        var moveResult = _service.MakeMove(roomId, new MakeMoveDto(new Position(1, 4), new Position(3, 4)));

        Assert.Multiple(() =>
        {
            Assert.That(moveResult.IsSuccess, Is.True);
            // SnapshotChanged is broadcasted on move
            _clientProxyMock.Verify(
                c => c.SendCoreAsync("SnapshotChanged", It.IsAny<object[]>(), default),
                Times.AtLeastOnce
            );
        });
    }

    #endregion

    #region State & Queries

    [Test]
    public void GetStatus_RoomDoesNotExist_Returns404NotFound()
    {
        var result = _service.GetStatus("UNKNOWN");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        });
    }

    [Test]
    public void GetStatus_RoomExists_ReturnsStatusDto()
    {
        CreateTestRoom("ON001", RoomMode.Online, r =>
        {
            r.WhiteJoined = true;
            r.WhiteName = "Alice";
        });

        var result = _service.GetStatus("ON001");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data!.Id, Is.EqualTo("ON001"));
            Assert.That(result.Data.WhiteName, Is.EqualTo("Alice"));
        });
    }

    [Test]
    public void GetState_RoomDoesNotExist_Returns404NotFound()
    {
        var result = _service.GetState("UNKNOWN");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        });
    }

    [Test]
    public void GetState_RoomExists_ReturnsGameState()
    {
        CreateTestRoom("HOT001", RoomMode.HotSeat, r => r.Engine.StartGame());

        var result = _service.GetState("HOT001");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data!.State, Is.EqualTo(GameState.InProgress));
        });
    }

    [Test]
    public void GetBoard_RoomDoesNotExist_Returns404NotFound()
    {
        var result = _service.GetBoard("UNKNOWN");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        });
    }

    [Test]
    public void GetBoard_RoomExists_Returns64Tiles()
    {
        CreateTestRoom("HOT001", RoomMode.HotSeat);

        var result = _service.GetBoard("HOT001");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data!, Has.Count.EqualTo(64));
        });
    }

    [Test]
    public void GetLegalMoves_RoomDoesNotExist_Returns404NotFound()
    {
        var result = _service.GetLegalMoves("UNKNOWN");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        });
    }

    [Test]
    public void GetLegalMoves_RoomExists_ReturnsLegalMoves()
    {
        CreateTestRoom("HOT001", RoomMode.HotSeat, r => r.Engine.StartGame());

        var result = _service.GetLegalMoves("HOT001");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data!.Moves, Is.Not.Empty);
        });
    }

    [Test]
    public void GetHistory_RoomDoesNotExist_Returns404NotFound()
    {
        var result = _service.GetHistory("UNKNOWN", null);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        });
    }

    [Test]
    public void GetHistory_AfterMoveMade_ReturnsMoveHistory()
    {
        CreateTestRoom("HOT001", RoomMode.HotSeat, r => r.Engine.StartGame());
        _service.MakeMove("HOT001", new MakeMoveDto(new Position(1, 4), new Position(3, 4)));

        var resultAll = _service.GetHistory("HOT001", null);
        var resultWhite = _service.GetHistory("HOT001", PieceColor.White);
        var resultBlack = _service.GetHistory("HOT001", PieceColor.Black);

        Assert.Multiple(() =>
        {
            Assert.That(resultAll.IsSuccess, Is.True);
            Assert.That(resultAll.Data, Has.Count.EqualTo(1));
            Assert.That(resultWhite.Data, Has.Count.EqualTo(1));
            Assert.That(resultBlack.Data, Is.Empty);
        });
    }

    #endregion
}

