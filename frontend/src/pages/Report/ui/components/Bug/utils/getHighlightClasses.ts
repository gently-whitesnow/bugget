const defaultHighlightClass = "border-transparent border-b-base-300";
const bugStepHighlightClass =
  "border-primary/80 ring-2 ring-primary/20 bg-base-200/40";

export const getHighlightClasses = (
  isHighlighted: boolean,
  activeClass = bugStepHighlightClass
) => {
  return isHighlighted ? activeClass : defaultHighlightClass;
};
