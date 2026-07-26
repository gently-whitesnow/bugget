const repositoryUrl = 'https://github.com/gently-whitesnow/bugget';

const features = [
  ['01', 'Репорт без догадок', 'Шаги, ожидаемый и фактический результат держатся в одном понятном контексте.'],
  ['02', 'Вложения рядом с задачей', 'Скриншоты и другие материалы не теряются в личных сообщениях и разрозненных папках.'],
  ['03', 'Обсуждение и ответственность', 'Команда обсуждает баг в карточке, а статус и исполнители остаются видимыми всем участникам.'],
  ['04', 'Свой контур', 'Разворачивайте у себя: данные и настройки остаются в инфраструктуре вашей команды.'],
];

const workflow = [
  ['01', 'Зафиксируйте', 'Тестировщик описывает сценарий и добавляет доказательства.'],
  ['02', 'Разберите', 'Разработчик видит воспроизведение, контекст и обсуждение в одной карточке.'],
  ['03', 'Проверьте', 'Команда возвращается к репорту, чтобы подтвердить исправление и сохранить историю.'],
];

const faq = [
  ['Что нужно для запуска?', 'Актуальная инструкция и состав сервисов находятся в README репозитория. Там же описаны Docker Compose, PostgreSQL, Redis и варианты авторизации.'],
  ['Можно ли использовать Bugget в своей инфраструктуре?', 'Да. Bugget рассчитан на self-hosted развёртывание: вы поднимаете сервисы в своём окружении и управляете данными сами.'],
  ['Какая лицензия у проекта?', 'Проект распространяется по лицензии MIT. Полный текст лицензии доступен в репозитории.'],
  ['Это облачный сервис с регистрацией?', 'Нет. Этот сайт рассказывает об open-source версии Bugget. У него нет SaaS-аккаунтов или удалённого рабочего пространства.'],
];

function Arrow() {
  return <svg aria-hidden="true" viewBox="0 0 18 18" fill="none"><path d="M3 9h11M10 4l5 5-5 5" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" /></svg>;
}

function Mark() {
  return <span className="mark" aria-hidden="true"><i /><i /><i /></span>;
}

export default function Home() {
  return <main>
    <a className="skip-link" href="#content">К содержанию</a>
    <header className="site-header container">
      <a className="brand" href="#top" aria-label="Bugget — на главную"><Mark />bugget</a>
      <nav aria-label="Основная навигация">
        <a href="#how">Как работает</a>
        <a href="#features">Возможности</a>
        <a href="#install">Установка</a>
      </nav>
      <a className="header-link" href={repositoryUrl} target="_blank" rel="noreferrer">GitHub <Arrow /></a>
    </header>

    <div id="top" className="hero-wrap">
      <section className="hero container" id="content">
        <div className="eyebrow"><span />Open source · Self-hosted · MIT</div>
        <h1>Баг-репорты,<br /><em>в которых всё на месте.</em></h1>
        <p className="hero-copy">Bugget помогает QA и продуктовым командам превращать найденные ошибки в ясный путь к исправлению — без потерянного контекста и бесконечных уточнений.</p>
        <div className="hero-actions">
          <a className="button button-primary" href={repositoryUrl} target="_blank" rel="noreferrer">Открыть репозиторий <Arrow /></a>
          <a className="text-link" href="#how">Посмотреть, как устроено <Arrow /></a>
        </div>
        <div className="hero-note"><span>◎</span> Ваши данные остаются в вашей инфраструктуре</div>
      </section>
      <div className="glow glow-one" /><div className="glow glow-two" />
    </div>

    <section className="proof container" aria-label="Состав хорошего баг-репорта">
      <p>Один репорт — одна понятная история</p>
      <div className="proof-items"><span>Контекст</span><b>→</b><span>Воспроизведение</span><b>→</b><span>Обсуждение</span><b>→</b><span>Исправление</span></div>
    </section>

    <section className="section problem" aria-labelledby="problem-title">
      <div className="container split-title">
        <p className="kicker">Знакомая проблема</p>
        <div><h2 id="problem-title">«Не воспроизводится» —<br />не ответ на баг.</h2><p className="lead">Когда шаги, ожидание, фактический результат и вложения живут отдельно, команда тратит время не на исправление, а на восстановление картины.</p></div>
      </div>
    </section>

    <section className="section interface-section" aria-labelledby="interface-title">
      <div className="container interface-intro"><p className="kicker">Рабочий контекст</p><h2 id="interface-title">Всё необходимое —<br />в карточке репорта.</h2></div>
      <div className="container product-shot" aria-label="Пример интерфейса Bugget">
        <div className="shot-bar"><span className="dot red" /><span className="dot yellow" /><span className="dot green" /><span className="shot-address">bugget / reports / BG-128</span></div>
        <div className="shot-body">
          <aside><div className="shot-logo"><Mark />bugget</div><span className="side-label">ПРОЕКТ</span><div className="side-active">▣ Баг-репорты <b>12</b></div><div className="side-item">◎ Мои задачи</div><div className="side-item">◌ Команда</div><span className="side-label second">ФИЛЬТРЫ</span><div className="side-item">Все открытые</div><div className="side-item">Назначенные мне</div></aside>
          <article><div className="crumb">Баг-репорты / <strong>BG-128</strong></div><div className="report-heading"><div><span className="status">ОТКРЫТ</span><h3>Не сохраняется адрес доставки после редактирования</h3></div><div className="avatar">АЛ</div></div><div className="report-meta"><span>Создан 24 июля</span><span>·</span><span>Приоритет: <b>обычный</b></span><span>·</span><span>Исполнитель: <b>не назначен</b></span></div><hr /><div className="report-grid"><div><h4>Шаги воспроизведения</h4><ol><li>Открыть оформление заказа</li><li>Изменить адрес доставки</li><li>Нажать «Сохранить»</li></ol></div><div><h4>Ожидаемый результат</h4><p>Новый адрес отображается в форме и используется в заказе.</p><h4>Фактический результат</h4><p>После обновления страницы возвращается прежний адрес.</p></div></div><div className="attachment"><span>▧</span><div><b>delivery-address.png</b><small>Скриншот · 248 КБ</small></div><button type="button">Открыть</button></div><div className="comment"><div className="avatar purple">МК</div><div><b>Мария К.</b><small>сегодня, 11:42</small><p>Воспроизводится в Chrome 126. Добавила шаги и скриншот.</p></div></div></article>
        </div>
      </div>
      <p className="container interface-caption">Визуальный пример отражает состав карточки Bugget: описание, шаги, ожидание, факт, вложения и обсуждение.</p>
    </section>

    <section className="section" id="how" aria-labelledby="how-title">
      <div className="container"><p className="kicker">Как это работает</p><h2 id="how-title">От находки до<br /><em>проверенного исправления.</em></h2><div className="workflow">{workflow.map(([num, title, text]) => <article key={num}><span>{num}</span><h3>{title}</h3><p>{text}</p></article>)}</div></div>
    </section>

    <section className="section features-section" id="features" aria-labelledby="features-title">
      <div className="container"><p className="kicker">Возможности</p><div className="features-heading"><h2 id="features-title">Меньше шума.<br />Больше ясности.</h2><p>Bugget не пытается быть всем сразу. Он собирает работу с багом в ясный, последовательный процесс.</p></div><div className="features">{features.map(([number, title, text]) => <article key={number}><span>{number}</span><h3>{title}</h3><p>{text}</p></article>)}</div></div>
    </section>

    <section className="section open-source" id="install" aria-labelledby="install-title">
      <div className="container install-card"><div><p className="kicker">Ваш сервер. Ваши правила.</p><h2 id="install-title">Open source<br />без компромиссов.</h2><p className="install-copy">Исходный код Bugget открыт под лицензией MIT. Разворачивайте проект в своём контуре, изучайте устройство и адаптируйте под процессы команды.</p><a className="button button-dark" href={`${repositoryUrl}#быстрый-старт`} target="_blank" rel="noreferrer">Перейти к инструкции <Arrow /></a></div><div className="install-side"><div className="license">MIT</div><div className="repo-line"><span>⌘</span><div><b>gently-whitesnow/bugget</b><small>Исходный код и документация</small></div></div><a href={repositoryUrl} target="_blank" rel="noreferrer">github.com/gently-whitesnow/bugget <Arrow /></a></div></div>
    </section>

    <section className="section faq" aria-labelledby="faq-title"><div className="container faq-grid"><div><p className="kicker">Вопросы</p><h2 id="faq-title">Перед<br />установкой.</h2></div><div>{faq.map(([question, answer]) => <details key={question}><summary>{question}<span>+</span></summary><p>{answer}</p></details>)}</div></div></section>

    <section className="authors"><div className="container"><p className="kicker">Люди за проектом</p><h2>Сделано людьми,<br /><em>которым важна ясность.</em></h2><div className="author-grid"><article><span>01</span><h3>Зайцев Александр</h3><p>Автор ядра и ключевой разработчик проекта</p><a href="https://t.me/gently_whitesnow" target="_blank" rel="noreferrer">@gently_whitesnow <Arrow /></a></article><article><span>02</span><h3>Мария Лобикова</h3><p>Менеджерка продукта, тестировщица и создательница пользовательского пути</p><a href="https://t.me/mar_lob" target="_blank" rel="noreferrer">@mar_lob <Arrow /></a></article><article><span>03</span><h3>Виктория Иванова</h3><p>Фронтенд-разработчица</p><a href="https://t.me/vikivivivi" target="_blank" rel="noreferrer">@vikivivivi <Arrow /></a></article></div></div></section>

    <footer><div className="container footer-inner"><a className="brand" href="#top"><Mark />bugget</a><p>Open-source инструмент для работы с баг-репортами.</p><div><a href={repositoryUrl} target="_blank" rel="noreferrer">GitHub</a><a href={`${repositoryUrl}/blob/master/LICENSE`} target="_blank" rel="noreferrer">MIT License</a></div></div></footer>
  </main>;
}
