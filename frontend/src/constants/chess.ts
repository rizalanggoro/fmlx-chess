import type { GameEndReason, PieceColor, PieceType, Position } from '@/lib/api'

export const FILES = 'abcdefgh'

export const sameSquare = (a: Position | null | undefined, b: Position) =>
  a != null && a.column === b.column && a.row === b.row

export const colorName = (color: PieceColor) =>
  color === 'White' ? 'Putih' : 'Hitam'

export const playerName = (
  color: PieceColor,
  white?: string | null,
  black?: string | null,
) => (color === 'White' ? white : black)

export const playerLabel = (
  color: PieceColor,
  white?: string | null,
  black?: string | null,
) => {
  const name = playerName(color, white, black)?.trim()
  return name ? `${colorName(color)} (${name})` : colorName(color)
}

export const PIECE_NAMES: Record<PieceType, string> = {
  King: 'Raja',
  Queen: 'Ratu',
  Rook: 'Benteng',
  Bishop: 'Menteri',
  Knight: 'Kuda',
  Pawn: 'Pion',
}

export const PIECE_IMAGE: Record<PieceType, Record<PieceColor, string>> = {
  King: { White: '/chess-pieces/white-king.svg', Black: '/chess-pieces/black-king.svg' },
  Queen: { White: '/chess-pieces/white-queen.svg', Black: '/chess-pieces/black-queen.svg' },
  Rook: { White: '/chess-pieces/white-rook.svg', Black: '/chess-pieces/black-rook.svg' },
  Bishop: { White: '/chess-pieces/white-bishop.svg', Black: '/chess-pieces/black-bishop.svg' },
  Knight: { White: '/chess-pieces/white-knight.svg', Black: '/chess-pieces/black-knight.svg' },
  Pawn: { White: '/chess-pieces/white-pawn.svg', Black: '/chess-pieces/black-pawn.svg' },
}

export const REASONS: Record<GameEndReason, string> = {
  Checkmate: 'Checkmate',
  DrawByStalemate: 'Stalemate',
  DrawByAgreement: 'Draw disepakati',
  DrawBy50MoveRule: 'Aturan 50 langkah',
  DrawBy75MoveRule: 'Aturan 75 langkah',
  DrawByDeadPosition: 'Posisi mati',
  DrawByThreefoldRepetition: 'Repetisi 3x',
  DrawByFivefoldRepetition: 'Repetisi 5x',
  PlayerResigned: 'Menyerah',
  TimeExpired: 'Waktu habis',
}
