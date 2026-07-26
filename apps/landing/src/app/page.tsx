import type { ReactNode } from 'react';
import Image from 'next/image';

const repositoryUrl = 'https://github.com/gently-whitesnow/bugget';

const why = [
  ['UX-first', 'Баг-репорт должен быть таким же удобным, как отправка сообщения.'],
  ['Один ответственный', 'Всегда видно, кто делает следующий шаг.'],
  ['Одна точка правды', 'Обсуждения, артефакты и история — в одном месте, а не в разрозненных тредах.'],
  ['Процесс', 'Мы берём стандарт баг-репорта и выстраиваем на его основе понятный процесс.'],
  ['Прозрачность', 'История событий и простые статусы — без бюрократии «ради статусов».'],
  ['Фокус', 'Мы фокусируемся только на баг-репортах. Это позволяет сделать их по-настоящему удобными.'],
];

const faq = [
  ['Как установить bugreport?', 'Актуальная инструкция развёртывания и состав сервисов находятся в README репозитория. bugreport рассчитан на self-hosted установку в вашей инфраструктуре.'],
  ['Что должно быть в хорошем баг-репорте?', 'Заголовок «что сломано + где», контекст, шаги воспроизведения, ожидаемый и фактический результат, плюс артефакты: скриншоты или логи.'],
  ['Зачем один ответственный за репорт?', 'Чтобы всегда было понятно, кто делает следующий шаг. Ответственность закреплена на репорте — баги внутри не «размывают» владельца.'],
  ['Как bugreport уменьшает трение при создании?', 'Минимум полей, вложения, понятная история событий и единый поток обсуждения.'],
  ['Безопасно ли хранить баг-репорты в одном месте?', 'Это безопаснее, чем раскидывать детали по чатам и документам. Не добавляйте пароли, токены и приватные ключи в текст или логи.'],
];

function GitHubLink({ children, className }: { children: ReactNode; className?: string }) {
  return <a className={className} href={repositoryUrl} target="_blank" rel="noreferrer">{children}</a>;
}

export default function Home() {
  return <main className="page-shell">
    <a className="skip-link" href="#content">К содержанию</a>
    <header className="header">
      <div className="header-inner">
        <a className="brand" href="#top" aria-label="bugreport — на главную"><Image className="brand-icon" src="/android-chrome-192x192.png" width={20} height={20} alt="" />bugreport</a>
        <nav aria-label="Основная навигация"><a href="#how">Как это работает</a><a href="#why">Почему bugreport</a><a href="#faq">FAQ</a></nav>
        <GitHubLink className="button button-small">GitHub</GitHubLink>
      </div>
    </header>

    <div className="content" id="top">
      <section className="hero" id="content">
        <h1>Просто баг-репорт</h1>
        <p className="intro">Мы создаём процесс работы с багами, делая упор на удобство и эффективность.</p>
        <div className="cta-row"><GitHubLink className="button button-primary">Открыть репозиторий</GitHubLink><a className="button button-secondary" href={`${repositoryUrl}#быстрый-старт`} target="_blank" rel="noreferrer">Инструкция установки</a></div>
        <Image className="example-image" src="/example.png" width={2940} height={1502} priority alt="Интерфейс bugreport: репорт с багами, шагами, ожидаемым и фактическим результатом" />
      </section>

      <section id="how">
        <h2>Как это работает</h2>
        <div className="card-grid three"><article className="card"><h3>1) Описываете репорт</h3><p>Фиксируете, что сломано и где это произошло.</p></article><article className="card"><h3>2) Заполняете по минимуму</h3><p>Контекст → шаги → ОР/ФР → артефакты. Всё находится в одной карточке.</p></article><article className="card"><h3>3) Работаете вместе</h3><p>Команда видит один поток обсуждения, историю, статус и следующего ответственного.</p></article></div>
        <p className="note">Основа хорошего репорта — воспроизводимость: шаги, ожидаемый и фактический результат.</p>
      </section>

      <section id="why">
        <h2>Почему bugreport</h2>
        <div className="card-grid three accent-grid">{why.map(([title, text]) => <article className="card" key={title}><h3>{title}</h3><p>{text}</p></article>)}</div>
      </section>

      <section>
        <h2>Кому подходит</h2>
        <div className="card-grid three"><article className="card"><h3>Продуктовые команды</h3><p>Единый формат репортов и понятная ответственность — баги не теряются между спринтами.</p></article><article className="card"><h3>Стартапы</h3><p>Собирайте обратную связь по качеству продукта быстро и в одном месте — без тяжёлого трекера.</p></article><article className="card"><h3>Команды без выделенного QA</h3><p>Структурированные репорты помогают разработчикам самим вести процесс работы с багами.</p></article></div>
      </section>

      <section>
        <h2>Мини-шаблон баг-репорта</h2>
        <div className="card-grid two template-grid"><article className="card"><h3>Что писать</h3><ol><li>Заголовок: что сломано + где.</li><li>Контекст: платформа, среда, роль пользователя.</li><li>Шаги воспроизведения: коротко и по порядку.</li><li>ОР/ФР: ожидаемый и фактический результат.</li><li>Артефакты: скриншоты, логи или видео при необходимости.</li></ol></article><article className="card"><h3>Почему именно так</h3><p>Большие команды сходятся в одном: репорт ускоряет фикс, когда его можно быстро воспроизвести и сравнить ОР/ФР.</p><p className="small"><a href="https://developer.mozilla.org/en/docs/Mozilla/QA/Bug_writing_guidelines" target="_blank" rel="noreferrer">Mozilla: Bug Writing Guidelines</a><br /><a href="https://www.chromium.org/for-testers/bug-reporting-guidelines/" target="_blank" rel="noreferrer">Chromium: Bug Reporting Guidelines</a><br /><a href="https://webkit.org/bug-report-guidelines/" target="_blank" rel="noreferrer">WebKit: Bug Report Guidelines</a></p></article></div>
      </section>

      <section className="install-callout"><h2>Разверните bugreport у себя</h2><p>Исходный код открыт под лицензией MIT. Данные и настройки остаются в инфраструктуре вашей команды.</p><div className="cta-row"><GitHubLink className="button button-primary">Открыть репозиторий</GitHubLink><a className="button button-secondary" href={`${repositoryUrl}/blob/master/LICENSE`} target="_blank" rel="noreferrer">Лицензия MIT</a></div></section>

      <section id="faq"><h2>FAQ</h2><dl className="faq-list">{faq.map(([question, answer]) => <div key={question}><dt>{question}</dt><dd>{answer}</dd></div>)}</dl></section>

      <section className="authors"><h2>Авторы проекта</h2><div className="card-grid three"><article className="card"><h3>Зайцев Александр</h3><p>Автор ядра и ключевой разработчик проекта.</p><a href="https://t.me/gently_whitesnow" target="_blank" rel="noreferrer">@gently_whitesnow</a></article><article className="card"><h3>Мария Лобикова</h3><p>Менеджерка продукта, тестировщица и создательница пользовательского пути.</p><a href="https://t.me/mar_lob" target="_blank" rel="noreferrer">@mar_lob</a></article><article className="card"><h3>Виктория Иванова</h3><p>Фронтенд-разработчица.</p><a href="https://t.me/vikivivivi" target="_blank" rel="noreferrer">@vikivivivi</a></article></div></section>
    </div>
    <footer><div><span>© 2026 bugreport</span><GitHubLink>GitHub</GitHubLink><a href={`${repositoryUrl}/blob/master/LICENSE`} target="_blank" rel="noreferrer">MIT License</a></div></footer>
  </main>;
}
