import React from "react";

type Props = {
  isChanged: boolean;
  onReset: () => void;
};

const CancelButton: React.FC<Props> = ({ isChanged, onReset }) => {
  return (
    <button onClick={onReset} className={`btn px-4 py-2`} disabled={!isChanged}>
      Отменить
    </button>
  );
};

export default CancelButton;
