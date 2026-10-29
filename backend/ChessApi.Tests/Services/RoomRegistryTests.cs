using Api.Enums;
using Api.Interfaces.Services;
using Api.Models;
using Api.Services;
using Moq;

namespace ChessApi.Tests.Services;

[TestFixture]
public class RoomRegistryTests
{
    private RoomRegistry _registry = null!;

    [SetUp]
    public void SetUp()
    {
        _registry = new RoomRegistry();
    }

    [Test]
    public void Get_RoomDoesNotExist_ReturnsNull()
    {
        var result = _registry.Get("NONEXISTENT");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Get_RoomExists_ReturnsMatchingRoom()
    {
        var room = new Room("ROOM01", RoomMode.HotSeat, Mock.Of<IChessGameEngine>());
        _registry.Create(room);

        var result = _registry.Get("ROOM01");

        Assert.That(result, Is.SameAs(room));
    }

    [Test]
    public void Create_NewRoom_AddsRoomToRegistry()
    {
        var room = new Room("ROOM02", RoomMode.Online, Mock.Of<IChessGameEngine>());

        _registry.Create(room);

        Assert.That(_registry.Get("ROOM02"), Is.SameAs(room));
    }

    [Test]
    public void Create_ExistingRoomId_OverwritesRoom()
    {
        var room1 = new Room("SAMEID", RoomMode.HotSeat, Mock.Of<IChessGameEngine>());
        var room2 = new Room("SAMEID", RoomMode.Online, Mock.Of<IChessGameEngine>());

        _registry.Create(room1);
        _registry.Create(room2);

        Assert.That(_registry.Get("SAMEID"), Is.SameAs(room2));
    }

    [Test]
    public void All_WhenNoRoomsExist_ReturnsEmptyCollection()
    {
        var allRooms = _registry.All;

        Assert.That(allRooms, Is.Empty);
    }

    [Test]
    public void All_WhenRoomsExist_ReturnsAllRegisteredRooms()
    {
        var room1 = new Room("ROOM01", RoomMode.HotSeat, Mock.Of<IChessGameEngine>());
        var room2 = new Room("ROOM02", RoomMode.Online, Mock.Of<IChessGameEngine>());

        _registry.Create(room1);
        _registry.Create(room2);

        var allRooms = _registry.All.ToList();

        Assert.Multiple(() =>
        {
            Assert.That(allRooms, Has.Count.EqualTo(2));
            Assert.That(allRooms, Does.Contain(room1));
            Assert.That(allRooms, Does.Contain(room2));
        });
    }
}

