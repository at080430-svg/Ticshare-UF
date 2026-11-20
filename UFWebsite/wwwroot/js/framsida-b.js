// Prototype B extras: live numbers and the "next match" card in the dark hero.
// Uses the same /api/matches endpoint as script.js, so every number is real.
async function fyllHero() {
    let matcher;
    try {
        matcher = await api("/api/matches?sport=Alla");
    } catch {
        return; // keep the hero as it is if the API fails
    }

    const medBiljetter = matcher.filter(m => m.ticketsForSale > 0);
    const antalBiljetter = medBiljetter.reduce((summa, m) => summa + m.ticketsForSale, 0);

    // Only show the live line when there is something to show
    if (antalBiljetter > 0) {
        document.getElementById("live-biljetter").textContent = antalBiljetter;
        document.getElementById("live-matcher").textContent = medBiljetter.length;
        document.getElementById("live-rad").hidden = false;
    }

    const kort = document.getElementById("nasta-match");
    const nasta = medBiljetter.sort((a, b) => new Date(a.startTime) - new Date(b.startTime))[0];
    if (!nasta) {
        document.getElementById("nasta-lag").textContent = "Nya matcher läggs upp hela tiden";
        return;
    }

    kort.href = `match.html?id=${nasta.id}`;
    kort.classList.add(sportKlass(nasta.sport));
    document.getElementById("nasta-lag").textContent = `${nasta.home} – ${nasta.away}`;
    document.getElementById("nasta-info").textContent =
        `${datum(nasta.startTime)} · ${nasta.arena}${nasta.city ? `, ${nasta.city}` : ""}`;
    document.getElementById("nasta-pris").textContent = `Från ${kronor(nasta.lowestPrice)}`;
}

fyllHero();
