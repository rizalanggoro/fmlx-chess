import { ChessBoard } from '@/components/chess-board'
import { CapturedPiecesDialog } from '@/components/game/captured-pieces-dialog'
import { ConfirmDialog } from '@/components/game/confirm-dialog'
import { DrawOfferDialog } from '@/components/game/draw-offer-dialog'
import { MoveHistoryCard } from '@/components/game/move-history-card'
import { PlayerTurnCard } from '@/components/game/player-turn-card'
import { PromotionDialog } from '@/components/game/promotion-dialog'
import { Brand, Eyebrow, Shell, StateScreen } from '@/components/room-chrome'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import {
  getRoomsByRoomId,
  getRoomsByRoomIdGameBoardOptions,
  getRoomsByRoomIdGameHistoryOptions,
  getRoomsByRoomIdGameLegalMovesOptions,
  getRoomsByRoomIdGameOptions,
  getRoomsByRoomIdOptions,
  postRoomsByRoomIdGameDrawOfferMutation,
  postRoomsByRoomIdGameDrawRespondMutation,
  postRoomsByRoomIdGameMovesMutation,
  postRoomsByRoomIdGameResignMutation,
  postRoomsByRoomIdGameStartMutation,
} from '@/lib/api'
import { useRoomSnapshot } from '@/hooks/use-room-snapshot'
import { useCheckToast } from '@/hooks/use-check-toast'
import { useDrawOffer } from '@/hooks/use-draw-offer'
import { useGameEnded } from '@/hooks/use-game-ended'
import { usePieceCapturedToast } from '@/hooks/use-piece-captured-toast'
import { colorName } from '@/constants/chess'
import type { DrawOfferedDto, PieceColor, PieceType, Position } from '@/lib/api'
import { joinGroup, leaveGroup, roomGroup } from '@/lib/game-hub'
import { loadRoomSession, type RoomSession } from '@/lib/room-session'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { createFileRoute, redirect, useNavigate } from '@tanstack/react-router'
import { useEffect, useState } from 'react'

export const Route = createFileRoute('/rooms/$roomId/')({
  // Link room online tanpa session diarahkan ke daftar room; join lewat dialog di sana.
  beforeLoad: async ({ params }) => {
    if (loadRoomSession(params.roomId)) return
    const room = await getRoomsByRoomId({ path: { roomId: params.roomId } }).catch(() => null)
    if (room?.data?.mode === 'Online') throw redirect({ to: '/rooms' })
  },
  component: Room,
})

const same = (a: Position | null, b: Position) =>
  a !== null && a.column === b.column && a.row === b.row

function Room() {
  const { roomId } = Route.useParams()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [selected, setSelected] = useState<Position | null>(null)
  const [session] = useState<RoomSession | null>(() => loadRoomSession(roomId))
  const [copied, setCopied] = useState(false)
  // ponytail: pending draw offer is local state (API exposes none); persist it in GameStateDto if it must survive reload.
  const [offer, setOffer] = useState<DrawOfferedDto | null>(null)
  const [promotion, setPromotion] = useState<{
    from: Position
    to: Position
    options: PieceType[]
  } | null>(null)
  const [capturedView, setCapturedView] = useState<PieceColor | null>(null)
  const [capturedOpen, setCapturedOpen] = useState(false)

  useEffect(() => {
    const group = roomGroup(roomId)
    void joinGroup(group).then(() => void qc.invalidateQueries())
    return () => {
      void leaveGroup(group)
    }
  }, [roomId, qc])

  useGameEnded(roomId)
  useRoomSnapshot(roomId)
  useDrawOffer(roomId, session?.color, setOffer)
  usePieceCapturedToast(roomId, session?.color)
  useCheckToast(roomId, session?.color)

  const {
    data: room,
    isError: roomError
  } = useQuery({
    ...getRoomsByRoomIdOptions({ path: { roomId } }),
  })
  const { data: state } = useQuery({
    ...getRoomsByRoomIdGameOptions({ path: { roomId } }),
  })

  const online = room?.mode === 'Online'
  const waiting = online && !(room?.whiteJoined && room?.blackJoined)
  const myColor = session?.color
  const turn = state?.currentPlayer.color
  const over = state?.state === 'Completed'
  const myTurn = !online || turn === myColor
  const canAct = !online || myColor !== undefined
  const resignColor = online ? myColor : turn
  // Legal moves bergantung pada kotak yang dipilih; saat bukan giliran kita, pilihan lama diabaikan
  // supaya daftar langkah basi tidak tampil (tidak ada invalidasi query lagi).
  const activeSquare = myTurn ? selected : null
  const {
    data: board
  } = useQuery({
    ...getRoomsByRoomIdGameBoardOptions({ path: { roomId } }),
  })
  const {
    data: history
  } = useQuery({
    ...getRoomsByRoomIdGameHistoryOptions({ path: { roomId } }),
  })
  const {
    data: legal,
    isPending: legalPending
  } = useQuery({
    ...getRoomsByRoomIdGameLegalMovesOptions({
      path: { roomId },
      query: { column: activeSquare?.column, row: activeSquare?.row },
    }),
    enabled: activeSquare !== null,
  })

  const {
    mutate: makeMove,
    isPending: isMoving
  } = useMutation({
    ...postRoomsByRoomIdGameMovesMutation(),
  })
  const {
    mutate: start,
    isPending: isStarting
  } = useMutation({
    ...postRoomsByRoomIdGameStartMutation(),
  })
  const {
    mutate: resign,
    isPending: isResigning
  } = useMutation({
    ...postRoomsByRoomIdGameResignMutation(),
  })
  const {
    mutate: offerDraw,
    isPending: isOfferingDraw
  } = useMutation({
    ...postRoomsByRoomIdGameDrawOfferMutation(),
  })
  const {
    mutate: respondDraw,
    isPending: isRespondingDraw
  } = useMutation({
    ...postRoomsByRoomIdGameDrawRespondMutation(),
  })

  const nameOf = (color: PieceColor) =>
    color === 'White' ? room?.whiteName : room?.blackName
  const label = (color: PieceColor) =>
    nameOf(color) ? `${colorName(color)} (${nameOf(color)})` : colorName(color)

  const respond = (accepted: boolean) => {
    if (!myColor) return
    respondDraw(
      { path: { roomId }, body: { color: myColor, accepted } },
      { onSuccess: () => setOffer(null) },
    )
  }

  const copyCode = () =>
    navigator.clipboard?.writeText(roomId).then(
      () => {
        setCopied(true)
        setTimeout(() => setCopied(false), 1500)
      },
      () => {},
    )

  const pieces = new Map((board ?? []).map((t) => [`${t.column},${t.row}`, t]))
  const targets = new Set((legal ?? []).map((m) => `${m.to.column},${m.to.row}`))
  // Bidak yang sedang ditanyakan langkah legalnya: ditampilkan spinner di kotaknya.
  const loadingSquare =
    legalPending && activeSquare && pieces.get(`${activeSquare.column},${activeSquare.row}`)?.type
      ? activeSquare
      : null
  // Bidak yang dimakan tiap warna, dari tracker backend (GameStateDto), bukan dihitung ulang dari papan.
  const capturedOf = (color: PieceColor) =>
    (color === 'White' ? state?.capturedByWhite : state?.capturedByBlack) ?? []

  const openCaptured = (color: PieceColor) => {
    setCapturedView(color)
    setCapturedOpen(true)
  }

  // Black sees the board from Black's side.
  const flip = myColor === 'Black'

  const moves = history ?? []

  const click = (sq: Position) => {
    if (state?.state !== 'InProgress' || isMoving || !myTurn) return
    const key = `${sq.column},${sq.row}`
    if (activeSquare && targets.has(key)) {
      // Server menandai langkah promosi lewat PromotionType; pilih dulu sebelum kirim move.
      const options = (legal ?? []).flatMap((m) =>
        same(m.to, sq) && m.promotionType ? [m.promotionType] : [],
      )
      if (options.length > 0) {
        setPromotion({ from: activeSquare, to: sq, options })
        return
      }
      makeMove({
        path: { roomId },
        body: { from: activeSquare, to: sq, promotionType: null },
        query: myColor ? { color: myColor } : undefined,
      })
      setSelected(null)
      return
    }
    const piece = pieces.get(key)
    if (piece?.color && piece.color === turn) {
      setSelected(same(selected, sq) ? null : sq)
    } else {
      setSelected(null)
    }
  }

  const promote = (type: PieceType) => {
    if (!promotion) return
    makeMove({
      path: { roomId },
      body: { from: promotion.from, to: promotion.to, promotionType: type },
      query: myColor ? { color: myColor } : undefined,
    })
    setPromotion(null)
    setSelected(null)
  }

  const toLobby = () => void navigate({ to: '/' })

  if (roomError) {
    return (
      <StateScreen>
        <Card className="w-full text-center">
          <CardHeader>
            <Eyebrow>Room</Eyebrow>
            <CardTitle className="text-2xl">Room tidak ditemukan</CardTitle>
            <CardDescription>Cek lagi kodenya, atau buat room baru dari lobby.</CardDescription>
          </CardHeader>
          <CardContent>
            <Button className="w-full" onClick={toLobby}>
              Kembali ke Lobby
            </Button>
          </CardContent>
        </Card>
      </StateScreen>
    )
  }

  if (!room) {
    return (
      <Shell>
        <p className="text-center text-muted-foreground">Memuat…</p>
      </Shell>
    )
  }

  if (waiting) {
    return (
      <StateScreen>
        <Card className="w-full text-center">
          <CardHeader>
            <Eyebrow>Online</Eyebrow>
            <CardTitle className="text-2xl">Menunggu lawan bergabung…</CardTitle>
            <CardDescription>Bagikan kode room ini ke lawan.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <p className="font-mono text-3xl font-bold tracking-[0.25em] text-foreground">
              {roomId}
            </p>
            <div className="flex flex-wrap justify-center gap-2">
              <Button onClick={copyCode}>{copied ? 'Tersalin ✓' : 'Salin kode'}</Button>
              <Button variant="outline" onClick={toLobby}>
                Lobby
              </Button>
            </div>
            {myColor && (
              <p className="text-sm text-muted-foreground">
                Kamu main sebagai{' '}
                <span className="font-semibold text-foreground">{label(myColor)}</span>
              </p>
            )}
          </CardContent>
        </Card>
      </StateScreen>
    )
  }

  return (
    <Shell fill>
      <header className="flex flex-wrap items-end justify-between gap-4">
        <Brand />
      </header>

      <main className="grid grid-cols-1 gap-6 md:grid-cols-[minmax(0,1fr)_18rem] md:items-start lg:grid-cols-[minmax(0,1fr)_20rem]">
        <ChessBoard
          tiles={board ?? []}
          selected={activeSquare}
          loading={loadingSquare}
          targets={targets}
          flip={flip}
          lastMove={moves.at(-1)?.move}
          disabled={state?.state !== 'InProgress' || isMoving || !myTurn}
          checked={state?.checkedColor}
          onSelect={click}
        />

        <aside className="flex flex-col gap-4">
          <PlayerTurnCard
            roomId={roomId}
            online={online}
            state={state}
            whiteName={room?.whiteName}
            blackName={room?.blackName}
            myColor={myColor}
            onPlayerClick={openCaptured}
          />

          <div className="flex flex-col gap-2">
            <div className="flex gap-2">
              {canAct && (
                <ConfirmDialog
                  className="flex-1"
                  label="Game baru"
                  disabled={isStarting}
                  title="Mulai game baru?"
                  description="Papan dan riwayat langkah akan direset dari awal."
                  confirmLabel="Ya, mulai baru"
                  onConfirm={() => start({ path: { roomId } })}
                />
              )}
              <ConfirmDialog
                className="flex-1"
                label="Lobby"
                variant="outline"
                title="Keluar ke lobby?"
                description="Room ini tetap hidup — kamu bisa masuk lagi pakai kode room."
                confirmLabel="Keluar ke lobby"
                onConfirm={toLobby}
              />
            </div>
            {!over && canAct && resignColor && (
              <ConfirmDialog
                className="w-full"
                label={`${colorName(resignColor)} menyerah`}
                variant="destructive"
                disabled={isResigning}
                title="Menyerah?"
                description={`${colorName(resignColor)} akan dinyatakan kalah dan game selesai.`}
                confirmLabel="Ya, menyerah"
                confirmVariant="destructive"
                onConfirm={() =>
                  resign({
                    path: { roomId },
                    body: { color: resignColor },
                  })
                }
              />
            )}
            {online && !over && myColor && !offer && (
              <ConfirmDialog
                className="w-full"
                label="Tawarkan draw"
                variant="outline"
                disabled={isOfferingDraw}
                title="Tawarkan draw?"
                description="Lawan bisa menerima untuk mengakhiri game seri, atau menolak."
                confirmLabel="Ya, tawarkan"
                onConfirm={() => offerDraw({ path: { roomId }, body: { color: myColor } })}
              />
            )}
          </div>

          <DrawOfferDialog
            open={!!offer && !over}
            offerer={offer ? label(offer.color) : 'Lawan'}
            pending={isRespondingDraw}
            onRespond={respond}
            onDismiss={() => setOffer(null)}
          />

          <PromotionDialog
            open={promotion !== null && !over}
            color={turn ?? 'White'}
            options={promotion?.options ?? []}
            pending={isMoving}
            onSelect={promote}
            onDismiss={() => setPromotion(null)}
          />

          <CapturedPiecesDialog
            open={capturedOpen}
            onOpenChange={setCapturedOpen}
            color={capturedView ?? 'White'}
            playerName={capturedView ? (nameOf(capturedView) ?? colorName(capturedView)) : ''}
            pieces={capturedView ? capturedOf(capturedView) : []}
          />

          <MoveHistoryCard history={moves} />
        </aside>
      </main>
    </Shell>
  )
}
