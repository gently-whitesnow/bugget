import { useEffect, useRef, useState } from "react";

const minZoomLevel = 1;
const maxZoomLevel = 4;
const zoomStep = 0.25;

const clampZoom = (value: number) =>
  Math.min(Math.max(value, minZoomLevel), maxZoomLevel);

type Props = {
  src: string;
  alt: string;
};

/**
 * Картинка в полноэкранном просмотре: зум колесом и клавишами, перетаскивание
 * мышью.
 *
 * Состояние трансформации живёт здесь и сбрасывается перемонтированием — родитель
 * даёт компоненту `key` вложения, поэтому переключение стрелками возвращает
 * масштаб к единице само.
 */
function ImageViewer({ src, alt }: Props) {
  const [zoomLevel, setZoomLevel] = useState(minZoomLevel);
  const [position, setPosition] = useState({ x: 0, y: 0 });
  const [isDragging, setIsDragging] = useState(false);
  const dragStartRef = useRef({ x: 0, y: 0 });

  const zoomBy = (delta: number) => {
    setZoomLevel((previousZoom) => {
      const newZoom = clampZoom(previousZoom + delta);
      if (newZoom === minZoomLevel) {
        setPosition({ x: 0, y: 0 });
      }
      return newZoom;
    });
  };

  const reset = () => {
    setZoomLevel(minZoomLevel);
    setPosition({ x: 0, y: 0 });
    setIsDragging(false);
  };

  const handleWheel = (event: React.WheelEvent<HTMLDivElement>) => {
    event.preventDefault();
    zoomBy(event.deltaY < 0 ? zoomStep : -zoomStep);
  };

  const handleMouseDown = (event: React.MouseEvent<HTMLDivElement>) => {
    if (zoomLevel <= minZoomLevel) return;

    setIsDragging(true);
    dragStartRef.current = {
      x: event.clientX - position.x,
      y: event.clientY - position.y,
    };
  };

  const handleMouseMove = (event: React.MouseEvent<HTMLDivElement>) => {
    if (!isDragging || zoomLevel <= minZoomLevel) return;

    setPosition({
      x: event.clientX - dragStartRef.current.x,
      y: event.clientY - dragStartRef.current.y,
    });
  };

  const handleMouseUp = () => {
    if (!isDragging) return;
    setIsDragging(false);
  };

  const handleDoubleClick = () => {
    if (zoomLevel === minZoomLevel) {
      setZoomLevel(2);
      return;
    }
    reset();
  };

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "+" || event.key === "=") {
        event.preventDefault();
        zoomBy(zoomStep);
      }

      if (event.key === "-" || event.key === "_") {
        event.preventDefault();
        zoomBy(-zoomStep);
      }

      if (event.key === "0") {
        event.preventDefault();
        reset();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, []);

  return (
    <div
      className="relative h-[88dvh] overflow-hidden rounded-box"
      style={{
        cursor: isDragging
          ? "grabbing"
          : zoomLevel > minZoomLevel
            ? "zoom-out"
            : "zoom-in",
      }}
      onWheel={handleWheel}
      onMouseDown={handleMouseDown}
      onMouseMove={handleMouseMove}
      onMouseUp={handleMouseUp}
      onMouseLeave={handleMouseUp}
      onDoubleClick={handleDoubleClick}
    >
      <img
        src={src}
        alt={alt}
        className="absolute left-1/2 top-1/2 max-h-full max-w-full select-none"
        style={{
          transform: `translate(-50%, -50%) translate(${position.x}px, ${position.y}px) scale(${zoomLevel})`,
          transformOrigin: "center center",
        }}
        draggable={false}
      />
    </div>
  );
}

export default ImageViewer;
