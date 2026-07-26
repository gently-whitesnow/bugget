import PlainUrl from "../PlainUrl";

const urlRegex = /(https?:\/\/[^\s]+|www\.[^\s]+)/g;

type Props = {
  text: string;
};

const PlainTextLinks = ({ text }: Props) => {
  const parts = text.split(urlRegex);

  return (
    <>
      {parts.map((part, index) => {
        if (part.match(urlRegex)) {
          return <PlainUrl key={index} url={part} />;
        }
        return <span key={index}>{part}</span>;
      })}
    </>
  );
};

export default PlainTextLinks;
