import { useState } from "react";
import defaultAvaSrc from "./default-ava.png";
import { RoundedSkeleton } from "../RoundedSkeleton/RoundedSkeleton";

type Props = {
  src?: string;
  width?: number;
};

const Avatar = ({ src = defaultAvaSrc, width = 8 }: Props) => {
  const [isLoaded, setIsLoaded] = useState(false);
  const size = `${width * 0.25}rem`;

  return (
    <div className="avatar block" style={{ width: size, height: size }}>
      <div className="rounded-full overflow-hidden" style={{ width: size }}>
        {!isLoaded && <RoundedSkeleton size={width} />}
        <img
          src={src}
          alt="ava"
          className={isLoaded ? "block" : "hidden"}
          onLoad={() => setIsLoaded(true)}
        />
      </div>
    </div>
  );
};

export default Avatar;
