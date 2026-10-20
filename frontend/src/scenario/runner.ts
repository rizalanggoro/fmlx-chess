import {
  getRoomsByRoomIdGame,
  getRoomsByRoomIdGameBoard,
  getRoomsByRoomIdGameHistory,
  postRooms,
  postRoomsByRoomIdGameDrawClaimFifty,
  postRoomsByRoomIdGameDrawClaimThreefold,
  postRoomsByRoomIdGameDrawOffer,
  postRoomsByRoomIdGameDrawRespond,
  postRoomsByRoomIdGameMoves,
  postRoomsByRoomIdGameReset,
  postRoomsByRoomIdGameResign,
  postRoomsByRoomIdGameStart,
  type GameStateDto,
  type Move,
  type MoveHistoryEntry,
  type TileDto,
} from '@/lib/api'
import { FILES } from '@/constants/chess'
import type { Scenario, ScenarioExpect } from './scenarios'

export type Snapshot = {
  state: GameStateDto
  board: TileDto[]
  history: MoveHistoryEntry[]
}

export type RunOutcome = {
  pass: boolean
  failures: string[]
  error?: string
  errorStatus: number | null
  snapshot?: Snapshot
}

export type RunOptions = {
  onReset?: () => void | Promise<void>
  onMove?: (move: Move) => void | Promise<void>
  moveDelayMs?: number
}

const wait = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms))

const messageOf = (error: unknown) => {
  if (error instanceof Error) return error.message
  try {
    return JSON.stringify(error)
  } catch {
    return String(error)
  }
}

const square = (value: string) => ({
  column: FILES.indexOf(value[0]),
  row: Number(value.slice(1)) - 1,
})

export async function createScenarioRoom() {
  const { data } = await postRooms({
    body: { mode: 'HotSeat' },
    throwOnError: true,
  })
  return data.id
}

export async function fetchSnapshot(roomId: string): Promise<Snapshot> {
  const [state, board, history] = await Promise.all([
    getRoomsByRoomIdGame({ path: { roomId }, throwOnError: true }),
    getRoomsByRoomIdGameBoard({ path: { roomId }, throwOnError: true }),
    getRoomsByRoomIdGameHistory({ path: { roomId }, throwOnError: true }),
  ])
  return { state: state.data, board: board.data, history: history.data }
}

export async function resetScenarioRoom(roomId: string) {
  await postRoomsByRoomIdGameReset({ path: { roomId }, throwOnError: true })
  await postRoomsByRoomIdGameStart({ path: { roomId }, throwOnError: true })
}

async function applyAction(roomId: string, action: NonNullable<Scenario['actions']>[number]) {
  const path = { roomId }
  switch (action.action) {
    case 'resign':
      await postRoomsByRoomIdGameResign({
        path,
        body: { color: action.color },
        throwOnError: true,
      })
      break
    case 'offer-draw':
      await postRoomsByRoomIdGameDrawOffer({
        path,
        body: { color: action.color },
        throwOnError: true,
      })
      break
    case 'respond-draw':
      await postRoomsByRoomIdGameDrawRespond({
        path,
        body: { color: action.color, accepted: action.accepted },
        throwOnError: true,
      })
      break
    case 'claim-threefold':
      await postRoomsByRoomIdGameDrawClaimThreefold({
        path,
        body: { color: action.color },
        throwOnError: true,
      })
      break
    case 'claim-fifty':
      await postRoomsByRoomIdGameDrawClaimFifty({
        path,
        body: { color: action.color },
        throwOnError: true,
      })
      break
  }
}

function checkExpect(expect: ScenarioExpect, snapshot: Snapshot): string[] {
  const failures: string[] = []
  const { state, board, history } = snapshot

  if (expect.state && state.state !== expect.state) {
    failures.push(`state: expected ${expect.state}, got ${state.state}`)
  }
  if (expect.currentPlayer && state.currentPlayer.color !== expect.currentPlayer) {
    failures.push(
      `currentPlayer: expected ${expect.currentPlayer}, got ${state.currentPlayer.color}`,
    )
  }
  if (expect.moveCount !== undefined && state.moveCount !== expect.moveCount) {
    failures.push(`moveCount: expected ${expect.moveCount}, got ${state.moveCount}`)
  }

  if (expect.result !== undefined) {
    if (expect.result === null) {
      if (state.result) failures.push(`result: expected null, got ${state.result.reason}`)
    } else if (!state.result) {
      failures.push(`result: expected ${expect.result.reason}, got null`)
    } else {
      if (state.result.reason !== expect.result.reason) {
        failures.push(
          `result.reason: expected ${expect.result.reason}, got ${state.result.reason}`,
        )
      }
      if (
        'winner' in expect.result &&
        (state.result.winner ?? null) !== (expect.result.winner ?? null)
      ) {
        failures.push(
          `result.winner: expected ${expect.result.winner ?? 'null'}, got ${
            state.result.winner ?? 'null'
          }`,
        )
      }
    }
  }

  if (expect.castlingRights) {
    for (const [key, value] of Object.entries(expect.castlingRights)) {
      const actual = state.castlingRights[key as keyof typeof state.castlingRights]
      if (actual !== value) {
        failures.push(`castlingRights.${key}: expected ${value}, got ${actual}`)
      }
    }
  }

  if (expect.enPassantTarget !== undefined) {
    const expected = expect.enPassantTarget
    const actual = state.enPassantTarget ?? null
    if (expected === null && actual !== null) {
      failures.push(
        `enPassantTarget: expected null, got ${FILES[actual.column]}${actual.row + 1}`,
      )
    } else if (
      expected !== null &&
      (actual === null || actual.column !== expected.column || actual.row !== expected.row)
    ) {
      failures.push(
        `enPassantTarget: expected ${FILES[expected.column]}${expected.row + 1}, got ${
          actual ? `${FILES[actual.column]}${actual.row + 1}` : 'null'
        }`,
      )
    }
  }

  for (const tile of expect.tiles ?? []) {
    const pos = square(tile.square)
    const actual = board.find((t) => t.column === pos.column && t.row === pos.row)
    if (tile.type === null) {
      if (actual?.type) {
        failures.push(`${tile.square}: expected empty, got ${actual.color} ${actual.type}`)
      }
      continue
    }
    if (!actual?.type) {
      failures.push(`${tile.square}: expected ${tile.color ?? ''} ${tile.type}, got empty`.trim())
      continue
    }
    if (tile.type && actual.type !== tile.type) {
      failures.push(`${tile.square}: expected ${tile.type}, got ${actual.type}`)
    }
    if (tile.color && actual.color !== tile.color) {
      failures.push(`${tile.square}: expected ${tile.color}, got ${actual.color}`)
    }
  }

  if (expect.lastPromotion) {
    const last = history.at(-1)
    if (!last?.move.promotionType) {
      failures.push(`lastPromotion: expected ${expect.lastPromotion}, got none`)
    } else if (last.move.promotionType !== expect.lastPromotion) {
      failures.push(
        `lastPromotion: expected ${expect.lastPromotion}, got ${last.move.promotionType}`,
      )
    }
  }

  return failures
}

export async function runScenario(
  roomId: string,
  scenario: Scenario,
  options: RunOptions = {},
): Promise<RunOutcome> {
  let errorStatus: number | null = null
  try {
    await resetScenarioRoom(roomId)
    await options.onReset?.()

    for (const move of scenario.moves ?? []) {
      const result = await postRoomsByRoomIdGameMoves({
        path: { roomId },
        body: move,
        throwOnError: false,
      })
      if (result.error) {
        if (scenario.expect.errorStatus === undefined) {
          throw new Error(messageOf(result.error))
        }
        errorStatus = result.response?.status ?? null
        break
      }
      await options.onMove?.(move)
      if (options.moveDelayMs) await wait(options.moveDelayMs)
    }

    if (errorStatus === null) {
      for (const action of scenario.actions ?? []) {
        await applyAction(roomId, action)
      }
    }
  } catch (error) {
    return {
      pass: false,
      failures: [],
      error: messageOf(error),
      errorStatus,
    }
  }

  let snapshot: Snapshot
  try {
    snapshot = await fetchSnapshot(roomId)
  } catch (error) {
    return {
      pass: false,
      failures: [],
      error: messageOf(error),
      errorStatus,
    }
  }

  const failures: string[] = []
  if (scenario.expect.errorStatus !== undefined) {
    if (errorStatus === null) {
      failures.push(`errorStatus: expected ${scenario.expect.errorStatus}, got success`)
    } else if (errorStatus !== scenario.expect.errorStatus) {
      failures.push(
        `errorStatus: expected ${scenario.expect.errorStatus}, got ${errorStatus}`,
      )
    }
  }

  failures.push(...checkExpect(scenario.expect, snapshot))
  return { pass: failures.length === 0, failures, errorStatus, snapshot }
}
