type Props = {
  size?: number;
};

export const RoundedSkeleton = ({ size = 8 }: Props) => (
  <div
    className="skeleton rounded-full shrink-0"
    style={{ width: `${size * 0.25}rem`, height: `${size * 0.25}rem` }}
  />
);
