import {ChessBoard} from '@/components/chess-board'
import {
  CapturedPiecesDialog,
  ConfirmDialog,
  DrawOfferDialog,
  MoveHistoryCard,
  PlayerTurnCard,
  PromotionDialog,
  RoomLoading,
  RoomNotFound,
  RoomWaiting,
} from '@/components/game'
import {Brand, Shell} from '@/components/room-chrome'
import {
  getRoomsByRoomId,
  getRoomsByRoomIdGameBoardOptions,
  getRoomsByRoomIdGameHistoryOptions,
  getRoomsByRoomIdGameLegalMovesAllOptions,
  getRoomsByRoomIdGameOptions,
  getRoomsByRoomIdOptions,
  postRoomsByRoomIdGameDrawClaimFiftyMutation,
  postRoomsByRoomIdGameDrawClaimThreefoldMutation,
  postRoomsByRoomIdGameDrawOfferMutation,
  postRoomsByRoomIdGameDrawRespondMutation,
  postRoomsByRoomIdGameMovesMutation,
  postRoomsByRoomIdGameResignMutation,
  postRoomsByRoomIdGameStartMutation,
} from '@/lib/api'
import {
  useCheckToast,
  useDrawOffer,
  useGameEnded,
  usePieceCapturedToast,
  useRoomGroup,
  useRoomSnapshot,
} from '@/hooks'
import {colorName} from '@/constants/chess'
import type {DrawOfferedDto, PieceColor, PieceType, Position} from '@/lib/api'
import {loadRoomSession, type RoomSession} from '@/lib/room-session'
import {useMutation, useQuery} from '@tanstack/react-query'
import {createFileRoute, redirect, useNavigate} from '@tanstack/react-router'
import {useState} from 'react'

export const Route = createFileRoute('/rooms/$roomId/')({
  beforeLoad: async ({params}) => {
    if (loadRoomSession(params.roomId))
      return

    const room = await getRoomsByRoomId({
      path: {
        roomId: params.roomId
      }
    }).catch(() => null)

    if (room?.data?.mode === 'Online')
      throw redirect({to: '/rooms'})
  },
  component: Page,
})

const same = (a: Position | null, b: Position) =>
  a !== null && a.column === b.column && a.row === b.row

function Page() {
  const {roomId} = Route.useParams()

  const navigate = useNavigate()
  const [selectedSquare, setSelectedSquare] = useState<Position | null>(null)
  const [roomSession] = useState<RoomSession | null>(() => loadRoomSession(roomId))
  const [drawOffer, setDrawOffer] = useState<DrawOfferedDto | null>(null)
  const [pendingPromotion, setPendingPromotion] = useState<{
    from: Position
    to: Position
    options: PieceType[]
  } | null>(null)
  const [capturedColor, setCapturedColor] = useState<PieceColor | null>(null)
  const [capturedDialogOpen, setCapturedDialogOpen] = useState(false)

  useRoomGroup(roomId)
  useGameEnded(roomId)
  useRoomSnapshot(roomId)
  useDrawOffer(roomId, roomSession?.color, setDrawOffer)
  usePieceCapturedToast(roomId, roomSession?.color)
  useCheckToast(roomId, roomSession?.color)

  const {
    data: room,
    isError: roomError
  } = useQuery({
    ...getRoomsByRoomIdOptions({path: {roomId}}),
  })
  const {data: state} = useQuery({
    ...getRoomsByRoomIdGameOptions({path: {roomId}}),
  })

  const online = room?.mode === 'Online'
  const waiting = online && !(room?.whiteJoined && room?.blackJoined)
  const myColor = roomSession?.color
  const turn = state?.currentPlayer.color
  const over = state?.state === 'Completed'
  const myTurn = !online || turn === myColor
  const canAct = !online || myColor !== undefined
  const resignColor = online ? myColor : turn
  const activeSquare = myTurn ? selectedSquare : null

  const {
    data: board
  } = useQuery({
    ...getRoomsByRoomIdGameBoardOptions({path: {roomId}}),
  })
  const {
    data: history
  } = useQuery({
    ...getRoomsByRoomIdGameHistoryOptions({path: {roomId}}),
  })
  const {
    data: legal,
    isPending: legalPending
  } = useQuery({
    ...getRoomsByRoomIdGameLegalMovesAllOptions({path: {roomId}}),
    enabled: state?.state === 'InProgress',
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
  const {
    mutate: claimThreefold,
    isPending: isClaimingThreefold
  } = useMutation({
    ...postRoomsByRoomIdGameDrawClaimThreefoldMutation(),
  })
  const {
    mutate: claimFifty,
    isPending: isClaimingFifty
  } = useMutation({
    ...postRoomsByRoomIdGameDrawClaimFiftyMutation(),
  })

  const nameOf = (color: PieceColor) =>
    color === 'White' ? room?.whiteName : room?.blackName
  const label = (color: PieceColor) =>
    nameOf(color) ? `${colorName(color)} (${nameOf(color)})` : colorName(color)

  const respond = (accepted: boolean) => {
    if (!myColor) return
    respondDraw(
      {path: {roomId}, body: {color: myColor, accepted}},
      {onSuccess: () => setDrawOffer(null)},
    )
  }

  const pieces = new Map((board ?? []).map((t) => [`${t.column},${t.row}`, t]))
  // Daftar langkah sisi yang sedang jalan; dipakai hanya kalau versinya cocok dengan state.
  const allLegalMoves =
    legal && legal.moveCount === state?.moveCount ? legal.moves : []
  const selectedMoves = activeSquare
    ? allLegalMoves.filter((m) => same(m.from, activeSquare))
    : []
  const targets = new Set(selectedMoves.map((m) => `${m.to.column},${m.to.row}`))
  // Spinner hanya saat prefetch awal belum selesai; setelah snapshot masuk daftar sudah ada.
  const loadingSquare =
    legalPending && activeSquare && pieces.get(`${activeSquare.column},${activeSquare.row}`)?.type
      ? activeSquare
      : null
  // Bidak yang dimakan tiap warna, dari tracker backend (GameStateDto), bukan dihitung ulang dari papan.
  const capturedOf = (color: PieceColor) =>
    (color === 'White' ? state?.capturedByWhite : state?.capturedByBlack) ?? []

  const openCaptured = (color: PieceColor) => {
    setCapturedColor(color)
    setCapturedDialogOpen(true)
  }

  // Black sees the board from Black's side.
  const flip = myColor === 'Black'

  const moves = history ?? []

  const click = (sq: Position) => {
    if (state?.state !== 'InProgress' || isMoving || !myTurn) return
    const key = `${sq.column},${sq.row}`
    if (activeSquare && targets.has(key)) {
      // Server menandai langkah promosi lewat PromotionType; pilih dulu sebelum kirim move.
      const options = selectedMoves.flatMap((m) =>
        same(m.to, sq) && m.promotionType ? [m.promotionType] : [],
      )
      if (options.length > 0) {
        setPendingPromotion({from: activeSquare, to: sq, options})
        return
      }
      makeMove({
        path: {roomId},
        body: {from: activeSquare, to: sq, promotionType: null},
        query: myColor ? {color: myColor} : undefined,
      })
      setSelectedSquare(null)
      return
    }
    const piece = pieces.get(key)
    if (piece?.color && piece.color === turn) {
      setSelectedSquare(same(selectedSquare, sq) ? null : sq)
    } else {
      setSelectedSquare(null)
    }
  }

  const promote = (type: PieceType) => {
    if (!pendingPromotion) return
    makeMove({
      path: {roomId},
      body: {
        from: pendingPromotion.from,
        to: pendingPromotion.to,
        promotionType: type,
      },
      query: myColor ? {color: myColor} : undefined,
    })
    setPendingPromotion(null)
    setSelectedSquare(null)
  }

  const toLobby = () => void navigate({to: '/'})

  if (roomError) {
    return <RoomNotFound onBack={toLobby}/>
  }

  if (!room) {
    return <RoomLoading/>
  }

  if (waiting) {
    return (
      <RoomWaiting
        roomId={roomId}
        playerLabel={myColor ? label(myColor) : undefined}
        onBack={toLobby}
      />
    )
  }

  return (
    <Shell fill>
      <header className="flex flex-wrap items-end justify-between gap-4">
        <Brand/>
      </header>

      <main
        className="grid grid-cols-1 gap-6 md:grid-cols-[minmax(0,1fr)_18rem] md:items-start lg:grid-cols-[minmax(0,1fr)_20rem]">
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
                  onConfirm={() => start({path: {roomId}})}
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
                    path: {roomId},
                    body: {color: resignColor},
                  })
                }
              />
            )}
            {online && !over && myColor && !drawOffer && (
              <ConfirmDialog
                className="w-full"
                label="Tawarkan draw"
                variant="outline"
                disabled={isOfferingDraw}
                title="Tawarkan draw?"
                description="Lawan bisa menerima untuk mengakhiri game seri, atau menolak."
                confirmLabel="Ya, tawarkan"
                onConfirm={() => offerDraw({path: {roomId}, body: {color: myColor}})}
              />
            )}
            {!over && canAct && myTurn && resignColor && state?.canClaimThreefoldRepetition && (
              <ConfirmDialog
                className="w-full"
                label="Klaim repetisi 3×"
                variant="outline"
                disabled={isClaimingThreefold}
                title="Klaim seri (repetisi 3×)?"
                description="Posisi yang sama sudah muncul tiga kali. Game akan berakhir seri."
                confirmLabel="Ya, klaim seri"
                onConfirm={() =>
                  claimThreefold({path: {roomId}, body: {color: resignColor}})
                }
              />
            )}
            {!over && canAct && myTurn && resignColor && state?.canClaimFiftyMoveRule && (
              <ConfirmDialog
                className="w-full"
                label="Klaim 50 langkah"
                variant="outline"
                disabled={isClaimingFifty}
                title="Klaim seri (aturan 50 langkah)?"
                description="Lima puluh langkah tiap sisi telah berjalan tanpa langkah bidak atau tangkapan. Game akan berakhir seri."
                confirmLabel="Ya, klaim seri"
                onConfirm={() => claimFifty({path: {roomId}, body: {color: resignColor}})}
              />
            )}
          </div>

          <DrawOfferDialog
            open={!!drawOffer && !over}
            offerer={drawOffer ? label(drawOffer.color) : 'Lawan'}
            pending={isRespondingDraw}
            onRespond={respond}
            onDismiss={() => setDrawOffer(null)}
          />

          <PromotionDialog
            open={pendingPromotion !== null && !over}
            color={turn ?? 'White'}
            options={pendingPromotion?.options ?? []}
            pending={isMoving}
            onSelect={promote}
            onDismiss={() => setPendingPromotion(null)}
          />

          <CapturedPiecesDialog
            open={capturedDialogOpen}
            onOpenChange={setCapturedDialogOpen}
            color={capturedColor ?? 'White'}
            playerName={capturedColor ? (nameOf(capturedColor) ?? colorName(capturedColor)) : ''}
            pieces={capturedColor ? capturedOf(capturedColor) : []}
          />

          <MoveHistoryCard history={moves}/>
        </aside>
      </main>
    </Shell>
  )
}
