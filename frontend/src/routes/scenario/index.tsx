import { ChessBoard } from '@/components/chess-board'
import { Brand, Eyebrow, Shell } from '@/components/room-chrome'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { ScrollArea } from '@/components/ui/scroll-area'
import { REASONS, sameSquare } from '@/constants/chess'
import {
  getRoomsByRoomIdGameBoardOptions,
  getRoomsByRoomIdGameHistoryOptions,
  getRoomsByRoomIdGameLegalMovesAllOptions,
  getRoomsByRoomIdGameOptions,
  postRoomsByRoomIdGameMoves,
  type Move,
  type Position,
} from '@/lib/api'
import { SCENARIOS, SCENARIO_GROUPS, type Scenario } from '@/scenario/scenarios'
import {
  createScenarioRoom,
  resetScenarioRoom,
  runScenario,
  type RunOutcome,
} from '@/scenario/runner'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { createFileRoute } from '@tanstack/react-router'
import { RiArrowRightLine } from '@remixicon/react'
import { useEffect, useMemo, useRef, useState } from 'react'

export const Route = createFileRoute('/scenario/')({ component: ScenarioPage })

const errorText = (error: unknown) => {
  if (error instanceof Error) return error.message
  try {
    return JSON.stringify(error)
  } catch {
    return String(error)
  }
}

function ScenarioPage() {
  const qc = useQueryClient()
  const [roomId, setRoomId] = useState<string | null>(null)
  const [activeId, setActiveId] = useState(SCENARIOS[0].id)
  const [selected, setSelected] = useState<Position | null>(null)
  const [lastMove, setLastMove] = useState<Move | null>(null)
  const [busy, setBusy] = useState(false)
  const [bootError, setBootError] = useState<string | null>(null)
  const [outcomes, setOutcomes] = useState<Record<string, RunOutcome>>({})
  const createRef = useRef<Promise<string> | null>(null)

  const ensureRoom = async () => {
    if (roomId) return roomId
    if (!createRef.current) {
      createRef.current = createScenarioRoom()
        .then((id) => {
          setRoomId(id)
          return id
        })
        .catch((error) => {
          createRef.current = null
          throw error
        })
    }
    return createRef.current
  }

  useEffect(() => {
    void ensureRoom().catch((error) => setBootError(errorText(error)))
    // Ensure the page has a room ready for both scripted and manual play.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const queryRoomId = roomId ?? ''
  const { data: state } = useQuery({
    ...getRoomsByRoomIdGameOptions({ path: { roomId: queryRoomId } }),
    enabled: !!roomId,
  })
  const { data: board } = useQuery({
    ...getRoomsByRoomIdGameBoardOptions({ path: { roomId: queryRoomId } }),
    enabled: !!roomId,
  })
  const { data: history } = useQuery({
    ...getRoomsByRoomIdGameHistoryOptions({ path: { roomId: queryRoomId } }),
    enabled: !!roomId,
  })
  const { data: legal } = useQuery({
    ...getRoomsByRoomIdGameLegalMovesAllOptions({ path: { roomId: queryRoomId } }),
    enabled: !!roomId && selected !== null,
  })

  const active = useMemo(
    () => SCENARIOS.find((scenario) => scenario.id === activeId) ?? SCENARIOS[0],
    [activeId],
  )
  const outcome = outcomes[active.id]
  const targets = new Set(
    (legal?.moves ?? [])
      .filter((move) => sameSquare(selected, move.from))
      .map((move) => `${move.to.column},${move.to.row}`),
  )
  const passed = Object.values(outcomes).filter((result) => result.pass).length

  const refresh = async () => {
    await qc.invalidateQueries()
  }

  const showMove = async (move: Move) => {
    setLastMove(move)
    await refresh()
  }

  const resetStep = async () => {
    setLastMove(null)
    await refresh()
  }

  const newRoom = async () => {
    setBusy(true)
    setBootError(null)
    setSelected(null)
    setLastMove(null)
    createRef.current = null
    try {
      const id = await createScenarioRoom()
      setRoomId(id)
      setOutcomes({})
      await refresh()
    } catch (error) {
      setBootError(errorText(error))
    } finally {
      setBusy(false)
    }
  }

  const reset = async () => {
    setBusy(true)
    setBootError(null)
    setSelected(null)
    setLastMove(null)
    try {
      const id = await ensureRoom()
      await resetScenarioRoom(id)
      setOutcomes({})
      await refresh()
    } catch (error) {
      setBootError(errorText(error))
    } finally {
      setBusy(false)
    }
  }

  const run = async (scenario: Scenario) => {
    setBusy(true)
    setBootError(null)
    setActiveId(scenario.id)
    setSelected(null)
    try {
      const id = await ensureRoom()
      const result = await runScenario(id, scenario, {
        onReset: resetStep,
        onMove: showMove,
        moveDelayMs: 420,
      })
      setOutcomes((prev) => ({ ...prev, [scenario.id]: result }))
      await refresh()
    } catch (error) {
      setBootError(errorText(error))
    } finally {
      setBusy(false)
    }
  }

  const runAll = async () => {
    setBusy(true)
    setBootError(null)
    setSelected(null)
    try {
      const id = await ensureRoom()
      for (const scenario of SCENARIOS) {
        setActiveId(scenario.id)
        const result = await runScenario(id, scenario, {
          onReset: resetStep,
          onMove: showMove,
          moveDelayMs: 300,
        })
        setOutcomes((prev) => ({ ...prev, [scenario.id]: result }))
      }
      await refresh()
    } catch (error) {
      setBootError(errorText(error))
    } finally {
      setBusy(false)
    }
  }

  const manualMove = async (from: Position, to: Position) => {
    setBusy(true)
    setBootError(null)
    setSelected(null)
    try {
      const id = await ensureRoom()
      await postRoomsByRoomIdGameMoves({
        path: { roomId: id },
        body: { from, to, promotionType: null },
        throwOnError: true,
      })
      await showMove({ from, to, promotionType: null })
    } catch (error) {
      setBootError(errorText(error))
    } finally {
      setBusy(false)
    }
  }

  const click = (position: Position) => {
    if (busy || !state || state.state !== 'InProgress') return
    const key = `${position.column},${position.row}`
    if (selected && targets.has(key)) {
      void manualMove(selected, position)
      return
    }
    const piece = (board ?? []).find(
      (tile) => tile.column === position.column && tile.row === position.row,
    )
    if (piece?.color === state.currentPlayer.color) {
      setSelected(sameSquare(selected, position) ? null : position)
    } else {
      setSelected(null)
    }
  }

  return (
    <Shell fill>
      <header className="flex flex-wrap items-end justify-between gap-4">
        <Brand />
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant="outline">dev tool</Badge>
          <Button variant="outline" size="sm" onClick={() => void newRoom()} disabled={busy}>
            Room baru
          </Button>
          <a
            href="/"
            className="inline-flex h-8 items-center rounded-4xl border border-border px-3 text-sm font-medium text-foreground hover:bg-muted"
          >
            Lobby
          </a>
        </div>
      </header>

      <main className="grid grid-cols-1 gap-6 md:grid-cols-[minmax(0,1fr)_22rem] md:items-start">
        <div className="flex flex-col gap-4">
          <ChessBoard
            tiles={board ?? []}
            selectedSquare={selected}
            legalTargets={targets}
            lastMove={lastMove}
            flipped={false}
            disabled={busy || state?.state !== 'InProgress'}
            onSquareSelect={click}
          />

          <Card size="sm">
            <CardHeader>
              <Eyebrow>Posisi</Eyebrow>
              <CardTitle className="font-heading text-xl">
                {state?.state === 'Completed'
                  ? state.result
                    ? REASONS[state.result.reason]
                    : 'Selesai'
                  : state?.state ?? 'Memuat…'}
              </CardTitle>
              <CardAction>
                <span className="font-mono text-xs tracking-[0.2em] text-muted-foreground">
                  {roomId ?? '…'}
                </span>
              </CardAction>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
              <div>
                <p className="text-xs text-muted-foreground">Giliran</p>
                <p className="font-medium">{state?.currentPlayer.color ?? '—'}</p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Langkah</p>
                <p className="font-medium">{state?.moveCount ?? 0}</p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Putih</p>
                <p className="font-mono">{state?.whiteRemainingSeconds ?? 0}s</p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Hitam</p>
                <p className="font-mono">{state?.blackRemainingSeconds ?? 0}s</p>
              </div>
              {state?.result && (
                <div className="col-span-2 sm:col-span-4">
                  <p className="text-xs text-muted-foreground">Hasil</p>
                  <p className="font-medium">
                    {state.result.winner
                      ? `${state.result.winner} menang — ${REASONS[state.result.reason]}`
                      : `Seri — ${REASONS[state.result.reason]}`}
                  </p>
                </div>
              )}
            </CardContent>
          </Card>

          <Card size="sm">
            <CardHeader>
              <CardTitle className="font-heading text-xl">Riwayat</CardTitle>
              <CardAction>
                <p className="text-xs text-muted-foreground">{history?.length ?? 0} langkah</p>
              </CardAction>
            </CardHeader>
            <CardContent>
              <ScrollArea className="h-40">
                <ol className="flex flex-col gap-1 font-mono text-xs">
                  {(history ?? []).map((entry, index) => (
                    <li key={index} className="flex gap-2">
                      <span className="w-8 text-muted-foreground">{index + 1}.</span>
                      <span>
                        {entry.playedBy} · {entry.move.from.column},{entry.move.from.row}
                        <RiArrowRightLine aria-hidden className="mx-0.5 inline size-3 shrink-0 text-muted-foreground" />
                        {entry.move.to.column},{entry.move.to.row}
                        {entry.move.promotionType ? `=${entry.move.promotionType}` : ''}
                      </span>
                    </li>
                  ))}
                  {(history ?? []).length === 0 && (
                    <li className="text-muted-foreground">Belum ada langkah.</li>
                  )}
                </ol>
              </ScrollArea>
            </CardContent>
          </Card>
        </div>

        <aside className="flex flex-col gap-4">
          <Card size="sm">
            <CardHeader>
              <Eyebrow>Scenario</Eyebrow>
              <CardTitle className="font-heading text-xl">Daftar uji</CardTitle>
              <CardDescription>
                Pilih skenario, lalu jalankan. Papan juga bisa diklik manual.
              </CardDescription>
            </CardHeader>
            <CardContent className="flex flex-col gap-3">
              <div className="flex gap-2">
                <Button className="flex-1" size="sm" onClick={() => void run(active)} disabled={busy}>
                  {busy ? 'Menjalankan…' : 'Run'}
                </Button>
                <Button
                  className="flex-1"
                  size="sm"
                  variant="outline"
                  onClick={() => void runAll()}
                  disabled={busy}
                >
                  Run all
                </Button>
              </div>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => void reset()}
                disabled={busy}
              >
                Reset papan
              </Button>
              <p className="text-xs text-muted-foreground">
                Lulus {passed}/{SCENARIOS.length}
              </p>
              <p className="text-xs text-muted-foreground">
                FE-only, memakai endpoint room yang ada. Posisi khusus, 50/75-move, dead
                position, dan timeout belum tercakup.
              </p>

              <ScrollArea className="h-[30rem]">
                <div className="flex flex-col gap-3 pr-2">
                  {SCENARIO_GROUPS.map((group) => {
                    const scenarios = SCENARIOS.filter((scenario) => scenario.group === group)
                    return (
                      <div key={group} className="flex flex-col gap-1">
                        <Eyebrow>{group}</Eyebrow>
                        {scenarios.map((scenario) => {
                          const result = outcomes[scenario.id]
                          const activeScenario = scenario.id === active.id
                          return (
                            <button
                              key={scenario.id}
                              type="button"
                              onClick={() => setActiveId(scenario.id)}
                              className={`flex items-center gap-2 rounded-lg border px-2.5 py-2 text-left text-sm transition-colors ${
                                activeScenario
                                  ? 'border-primary/30 bg-primary/10 text-foreground'
                                  : 'border-transparent hover:bg-muted'
                              }`}
                            >
                              <span className="min-w-0 flex-1 truncate">{scenario.name}</span>
                              {result && (
                                <Badge variant={result.pass ? 'secondary' : 'destructive'}>
                                  {result.pass ? '✓' : '✗'}
                                </Badge>
                              )}
                            </button>
                          )
                        })}
                      </div>
                    )
                  })}
                </div>
              </ScrollArea>
            </CardContent>
          </Card>

          <Card size="sm">
            <CardHeader>
              <Eyebrow>Hasil</Eyebrow>
              <CardTitle className="font-heading text-xl">{active.name}</CardTitle>
              <CardAction>
                {outcome ? (
                  <Badge variant={outcome.pass ? 'secondary' : 'destructive'}>
                    {outcome.pass ? 'Lulus' : 'Gagal'}
                  </Badge>
                ) : (
                  <Badge variant="outline">Belum</Badge>
                )}
              </CardAction>
            </CardHeader>
            <CardContent className="flex flex-col gap-3 text-sm">
              <p className="text-muted-foreground">{active.description}</p>
              {outcome?.error && <p className="text-destructive">{outcome.error}</p>}
              {outcome?.errorStatus !== null && outcome?.errorStatus !== undefined && (
                <p className="text-xs text-muted-foreground">
                  HTTP {outcome.errorStatus}
                </p>
              )}
              {outcome && !outcome.pass && outcome.failures.length > 0 && (
                <ul className="flex list-disc flex-col gap-1 pl-4 text-destructive">
                  {outcome.failures.map((failure) => (
                    <li key={failure}>{failure}</li>
                  ))}
                </ul>
              )}
              {outcome?.snapshot && (
                <div className="rounded-xl bg-muted p-3 text-xs">
                  <p>
                    state: <span className="font-mono">{outcome.snapshot.state.state}</span>
                  </p>
                  <p>
                    turn:{' '}
                    <span className="font-mono">
                      {outcome.snapshot.state.currentPlayer.color}
                    </span>
                  </p>
                  <p>
                    moves: <span className="font-mono">{outcome.snapshot.state.moveCount}</span>
                  </p>
                  <p>
                    result:{' '}
                    <span className="font-mono">
                      {outcome.snapshot.state.result
                        ? `${outcome.snapshot.state.result.reason}${
                            outcome.snapshot.state.result.winner
                              ? ` / ${outcome.snapshot.state.result.winner}`
                              : ''
                          }`
                        : 'null'}
                    </span>
                  </p>
                </div>
              )}
              {bootError && <p className="text-destructive">{bootError}</p>}
            </CardContent>
          </Card>
        </aside>
      </main>
    </Shell>
  )
}
