import type { ExternalSearchItem } from "../../../api/contracts";

type Props = {
  items: ExternalSearchItem[];
  onSelect: (item: ExternalSearchItem) => void;
};

const ExternalSearchResults = ({ items, onSelect }: Props) => {
  if (!items.length) return null;

  return (
    <div className="mt-2 rounded-2xl border border-base-300 bg-base-200">
      <div className="flex items-center justify-between px-4 py-2">
        <span className="text-sm font-medium">Внешний поиск</span>
      </div>
      {!!items.length && (
        <div className="divide-y divide-base-300">
          {items.map((item) => (
            <button
              key={`${item.source}-${item.id}`}
              type="button"
              className="w-full px-4 py-3 text-left transition hover:bg-base-300 cursor-pointer"
              onMouseDown={(e) => e.preventDefault()}
              onClick={() => onSelect(item)}
            >
              <div className="font-medium leading-snug">{item.text}</div>
              <div className="mt-1 text-xs uppercase tracking-wide text-base-content/60">
                {item.source}
              </div>
            </button>
          ))}
        </div>
      )}
    </div>
  );
};

export default ExternalSearchResults;
