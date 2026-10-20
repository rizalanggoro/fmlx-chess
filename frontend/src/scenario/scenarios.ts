import { FILES } from '@/constants/chess'
import type {
  CastlingRights,
  GameEndReason,
  GameState,
  Move,
  PieceColor,
  PieceType,
  Position,
} from '@/lib/api'

export type ScenarioAction =
  | { action: 'resign'; color: PieceColor }
  | { action: 'offer-draw'; color: PieceColor }
  | { action: 'respond-draw'; color: PieceColor; accepted: boolean }
  | { action: 'claim-threefold'; color: PieceColor }
  | { action: 'claim-fifty'; color: PieceColor }

export type ExpectedTile = {
  square: string
  color?: PieceColor | null
  type?: PieceType | null
}

export type ScenarioExpect = {
  state?: GameState
  currentPlayer?: PieceColor
  moveCount?: number
  result?: { reason: GameEndReason; winner?: PieceColor | null } | null
  castlingRights?: Partial<CastlingRights>
  enPassantTarget?: Position | null
  tiles?: ExpectedTile[]
  lastPromotion?: PieceType
  errorStatus?: number
}

export type Scenario = {
  id: string
  group: string
  name: string
  description: string
  moves?: Move[]
  actions?: ScenarioAction[]
  expect: ScenarioExpect
}

export const SCENARIO_GROUPS = [
  'Gerak',
  'Pawn',
  'Castling',
  'Check',
  'End',
  'Draw',
  'Aksi',
] as const

const sq = (square: string): Position => ({
  column: FILES.indexOf(square[0]),
  row: Number(square.slice(1)) - 1,
})

const m = (from: string, to: string, promotionType?: PieceType): Move => ({
  from: sq(from),
  to: sq(to),
  ...(promotionType ? { promotionType } : {}),
})

const knightCycle = [m('g1', 'f3'), m('g8', 'f6'), m('f3', 'g1'), m('f6', 'g8')]
const promotionBase = [
  m('a2', 'a4'),
  m('b8', 'c6'),
  m('a4', 'a5'),
  m('c6', 'b8'),
  m('a5', 'a6'),
  m('b8', 'c6'),
  m('a6', 'b7'),
  m('a7', 'a6'),
]

export const SCENARIOS: Scenario[] = [
  {
    id: 'pawn-double',
    group: 'Gerak',
    name: 'Pawn double step',
    description: 'Pawn putih maju dua kotak dari posisi awal.',
    moves: [m('e2', 'e4')],
    expect: {
      state: 'InProgress',
      currentPlayer: 'Black',
      moveCount: 1,
      tiles: [
        { square: 'e4', color: 'White', type: 'Pawn' },
        { square: 'e2', type: null },
      ],
    },
  },
  {
    id: 'capture',
    group: 'Gerak',
    name: 'Capture',
    description: 'Pawn putih menangkap pawn hitam di d5.',
    moves: [m('e2', 'e4'), m('d7', 'd5'), m('e4', 'd5')],
    expect: {
      state: 'InProgress',
      currentPlayer: 'Black',
      moveCount: 3,
      tiles: [
        { square: 'd5', color: 'White', type: 'Pawn' },
        { square: 'd7', type: null },
      ],
    },
  },
  {
    id: 'en-passant',
    group: 'Pawn',
    name: 'En passant',
    description: 'Pawn putih menangkap pawn hitam yang baru maju dua kotak.',
    moves: [m('e2', 'e4'), m('a7', 'a6'), m('e4', 'e5'), m('d7', 'd5'), m('e5', 'd6')],
    expect: {
      state: 'InProgress',
      currentPlayer: 'Black',
      moveCount: 5,
      tiles: [
        { square: 'd6', color: 'White', type: 'Pawn' },
        { square: 'd5', type: null },
        { square: 'e5', type: null },
      ],
    },
  },
  {
    id: 'promotion-queen',
    group: 'Pawn',
    name: 'Promotion → Queen',
    description: 'Pawn putih mencapai baris 8 dan menjadi queen.',
    moves: [...promotionBase, m('b7', 'b8', 'Queen')],
    expect: {
      state: 'InProgress',
      currentPlayer: 'Black',
      moveCount: 9,
      lastPromotion: 'Queen',
      tiles: [
        { square: 'b8', color: 'White', type: 'Queen' },
        { square: 'b7', type: null },
      ],
    },
  },
  {
    id: 'promotion-knight',
    group: 'Pawn',
    name: 'Promotion → Knight',
    description: 'Promotion tidak harus queen; knight juga harus diterima.',
    moves: [...promotionBase, m('b7', 'b8', 'Knight')],
    expect: {
      state: 'InProgress',
      currentPlayer: 'Black',
      moveCount: 9,
      lastPromotion: 'Knight',
      tiles: [
        { square: 'b8', color: 'White', type: 'Knight' },
        { square: 'b7', type: null },
      ],
    },
  },
  {
    id: 'castle-kingside',
    group: 'Castling',
    name: 'Castling kingside',
    description: 'Raja dan rook putih pindah ke sayap raja.',
    moves: [
      m('e2', 'e4'),
      m('e7', 'e5'),
      m('g1', 'f3'),
      m('b8', 'c6'),
      m('f1', 'c4'),
      m('f8', 'c5'),
      m('e1', 'g1'),
    ],
    expect: {
      state: 'InProgress',
      currentPlayer: 'Black',
      moveCount: 7,
      castlingRights: { whiteKingSide: false, whiteQueenSide: false },
      tiles: [
        { square: 'g1', color: 'White', type: 'King' },
        { square: 'f1', color: 'White', type: 'Rook' },
        { square: 'e1', type: null },
        { square: 'h1', type: null },
      ],
    },
  },
  {
    id: 'castle-queenside',
    group: 'Castling',
    name: 'Castling queenside',
    description: 'Raja dan rook putih pindah ke sayap ratu.',
    moves: [
      m('d2', 'd4'),
      m('d7', 'd5'),
      m('b1', 'c3'),
      m('b8', 'c6'),
      m('c1', 'f4'),
      m('c8', 'f5'),
      m('d1', 'd2'),
      m('d8', 'd7'),
      m('e1', 'c1'),
    ],
    expect: {
      state: 'InProgress',
      currentPlayer: 'Black',
      moveCount: 9,
      castlingRights: { whiteKingSide: false, whiteQueenSide: false },
      tiles: [
        { square: 'c1', color: 'White', type: 'King' },
        { square: 'd1', color: 'White', type: 'Rook' },
        { square: 'e1', type: null },
        { square: 'a1', type: null },
      ],
    },
  },
  {
    id: 'check',
    group: 'Check',
    name: 'Check',
    description: 'Bishop putih memberi skak ke raja hitam.',
    moves: [m('e2', 'e4'), m('d7', 'd5'), m('f1', 'b5')],
    expect: {
      state: 'InProgress',
      currentPlayer: 'Black',
      moveCount: 3,
      tiles: [{ square: 'b5', color: 'White', type: 'Bishop' }],
    },
  },
  {
    id: 'fools-mate',
    group: 'End',
    name: 'Checkmate',
    description: "Fool's mate: hitam checkmate di langkah keempat.",
    moves: [m('f2', 'f3'), m('e7', 'e5'), m('g2', 'g4'), m('d8', 'h4')],
    expect: {
      state: 'Completed',
      currentPlayer: 'Black',
      moveCount: 4,
      result: { reason: 'Checkmate', winner: 'Black' },
    },
  },
  {
    id: 'stalemate',
    group: 'End',
    name: 'Stalemate',
    description: 'Hitam tidak punya langkah legal, tapi raja tidak diskak.',
    moves: [
      m('e2', 'e3'),
      m('a7', 'a5'),
      m('d1', 'h5'),
      m('a8', 'a6'),
      m('h5', 'a5'),
      m('h7', 'h5'),
      m('a5', 'c7'),
      m('a6', 'h6'),
      m('h2', 'h4'),
      m('f7', 'f6'),
      m('c7', 'd7'),
      m('e8', 'f7'),
      m('d7', 'b7'),
      m('d8', 'd3'),
      m('b7', 'b8'),
      m('d3', 'h7'),
      m('b8', 'c8'),
      m('f7', 'g6'),
      m('c8', 'e6'),
    ],
    expect: {
      state: 'Completed',
      moveCount: 19,
      result: { reason: 'DrawByStalemate', winner: null },
    },
  },
  {
    id: 'threefold',
    group: 'Draw',
    name: 'Threefold repetition',
    description: 'Posisi yang sama muncul tiga kali, lalu draw diklaim.',
    moves: [...knightCycle, ...knightCycle],
    actions: [{ action: 'claim-threefold', color: 'White' }],
    expect: {
      state: 'Completed',
      moveCount: 8,
      result: { reason: 'DrawByThreefoldRepetition', winner: null },
    },
  },
  {
    id: 'draw-accept',
    group: 'Draw',
    name: 'Draw offer — accepted',
    description: 'Tawaran draw putih diterima hitam.',
    actions: [
      { action: 'offer-draw', color: 'White' },
      { action: 'respond-draw', color: 'Black', accepted: true },
    ],
    expect: {
      state: 'Completed',
      moveCount: 0,
      result: { reason: 'DrawByAgreement', winner: null },
    },
  },
  {
    id: 'draw-decline',
    group: 'Draw',
    name: 'Draw offer — declined',
    description: 'Tawaran draw ditolak, game tetap berjalan.',
    actions: [
      { action: 'offer-draw', color: 'White' },
      { action: 'respond-draw', color: 'Black', accepted: false },
    ],
    expect: {
      state: 'InProgress',
      currentPlayer: 'White',
      moveCount: 0,
      result: null,
    },
  },
  {
    id: 'resign',
    group: 'Aksi',
    name: 'Resign',
    description: 'Putih menyerah, hitam menang.',
    actions: [{ action: 'resign', color: 'White' }],
    expect: {
      state: 'Completed',
      moveCount: 0,
      result: { reason: 'PlayerResigned', winner: 'Black' },
    },
  },
  {
    id: 'illegal-move',
    group: 'Aksi',
    name: 'Illegal move',
    description: 'Hitam mencoba jalan saat giliran putih; API harus menolak.',
    moves: [m('e7', 'e5')],
    expect: {
      errorStatus: 400,
      state: 'InProgress',
      currentPlayer: 'White',
      moveCount: 0,
    },
  },
]
