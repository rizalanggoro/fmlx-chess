namespace Api.Enums;

public enum GameEndReason
{
    Checkmate,
    DrawByStalemate,
    DrawByAgreement,
    DrawBy50MoveRule,
    DrawBy75MoveRule,
    DrawByDeadPosition,
    DrawByThreefoldRepetition,
    DrawByFivefoldRepetition,
    PlayerResigned,
    TimeExpired
}
