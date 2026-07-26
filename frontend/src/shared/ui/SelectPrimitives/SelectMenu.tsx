import { ReactNode } from "react";

type Props<T> = {
  children?: ReactNode;
  className?: string;
  items?: T[];
  listClassName?: string;
  beforeItems?: ReactNode;
  afterItems?: ReactNode;
  renderItem?: (item: T, index: number) => ReactNode;
};

const SelectMenu = <T,>({
  children,
  className = "",
  items,
  listClassName = "",
  beforeItems,
  afterItems,
  renderItem,
}: Props<T>) => {
  const hasRenderableItems = items && renderItem;

  return (
    <div
      className={`absolute left-0 top-full z-20 mt-1 w-full rounded-[14px] border border-base-content/15 bg-base-100 p-1.5 shadow-xl ${className}`}
    >
      {beforeItems}
      {hasRenderableItems ? (
        <div className={listClassName}>
          {items.map((item, index) => renderItem(item, index))}
        </div>
      ) : (
        children
      )}
      {afterItems}
    </div>
  );
};

export default SelectMenu;
