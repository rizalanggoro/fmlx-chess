export function BoardBand() {
  return (
    <div
      aria-hidden
      className="flex w-fit flex-col overflow-hidden rounded-[3px] border border-amber-950/20"
    >
      {[0, 1].map((row) => (
        <div key={row} className="flex">
          {Array.from({ length: 8 }, (_, i) => (
            <span
              key={i}
              className={`h-2 w-4 ${(i + row) % 2 === 0 ? 'bg-amber-100' : 'bg-amber-800'}`}
            />
          ))}
        </div>
      ))}
    </div>
  )
}
