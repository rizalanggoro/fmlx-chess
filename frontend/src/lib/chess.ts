import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

const API = import.meta.env.VITE_API_URL ?? 'http://localhost:5004'

export type PieceColor = 'White' | 'Black'
export type PieceType = 'King' | 'Queen' | 'Rook' | 'Knight' | 'Bishop' | 'Pawn'
export type GameState = 'NotStarted' | 'InProgress' | 'Completed'
export type GameEndReason =
  | 'Checkmate'
  | 'DrawByStalemate'
  | 'DrawByAgreement'
  | 'DrawBy50MoveRule'
  | 'DrawBy75MoveRule'
  | 'DrawByDeadPosition'
  | 'DrawByThreefoldRepetition'
  | 'DrawByFivefoldRepetition'
  | 'PlayerResigned'
  | 'TimeExpired'

export interface Position {
  column: number
  row: number
}

export interface ChessMove {
  from: Position
  to: Position
  promotionType: PieceType | null
}

export interface TileDto extends Position {
  color: PieceColor | null
  type: PieceType | null
}

export interface PlayerDto {
  name: string
  color: PieceColor
}

export interface GameResultDto {
  reason: GameEndReason
  winner: PieceColor | null
}

export interface GameStateDto {
  state: GameState
  currentPlayer: PlayerDto
  result: GameResultDto | null
  enPassantTarget: Position | null
  moveCount: number
}

export interface MoveHistoryEntry {
  move: ChessMove
  playedBy: PieceColor
}

async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API}${path}`, {
    headers: { 'Content-Type': 'application/json' },
    ...init,
  })
  if (!res.ok) throw new Error(`API ${res.status}: ${path}`)
  return res.json() as Promise<T>
}

export const fetchState = () => api<GameStateDto>('/api/Game')
export const startGame = () => api<GameStateDto>('/api/Game/start', { method: 'POST' })
export const resetGame = () => api<GameStateDto>('/api/Game/reset', { method: 'POST' })
export const fetchBoard = () => api<TileDto[]>('/api/Game/board')
export const fetchHistory = () => api<MoveHistoryEntry[]>('/api/Game/history')
export const fetchLegalMoves = (column: number, row: number) =>
  api<ChessMove[]>(`/api/Game/legal-moves?column=${column}&row=${row}`)
export const postMove = (move: ChessMove) =>
  api<GameStateDto>('/api/Game/moves', { method: 'POST', body: JSON.stringify(move) })
export const resign = (color: PieceColor) =>
  api<GameStateDto>('/api/Game/resign', { method: 'POST', body: JSON.stringify({ color }) })

export function useGameState() {
  return useQuery({ queryKey: ['game-state'], queryFn: fetchState })
}

export function useBoard() {
  return useQuery({ queryKey: ['board'], queryFn: fetchBoard })
}

export function useHistory() {
  return useQuery({ queryKey: ['history'], queryFn: fetchHistory })
}

export function useLegalMoves(pos: Position | null) {
  return useQuery({
    queryKey: ['legal-moves', pos?.column, pos?.row],
    queryFn: () => fetchLegalMoves(pos!.column, pos!.row),
    enabled: pos !== null,
  })
}

function useGameMutation<TData, TVars>(fn: (v: TVars) => Promise<TData>) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      void qc.invalidateQueries()
    },
  })
}

export const useStartGame = () => useGameMutation(startGame)
export const useResetGame = () => useGameMutation(resetGame)
export const useMakeMove = () => useGameMutation(postMove)
export const useResign = () => useGameMutation(resign)
