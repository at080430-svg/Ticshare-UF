// Start page: search bar, sport filter buttons and the list of upcoming matches.
const filter = { sport: "Alla", q: "", city: "" };

async function visaMatcher() {
    const lista = document.getElementById("matchlista");
    const url = `/api/matches?sport=${encodeURIComponent(filter.sport)}`
        + `&q=${encodeURIComponent(filter.q)}&city=${encodeURIComponent(filter.city)}`;
    const matcher = await api(url);

    if (matcher.length === 0) {
        lista.innerHTML = filter.q || filter.city
            ? "<p>Inga matcher matchar din sökning.</p>"
            : "<p>Inga matcher just nu.</p>";
        return;
    }
    lista.innerHTML = matcher.map(m => `
        <a class="matchkort" href="match.html?id=${m.id}">
            <h3>${esc(m.home)} – ${esc(m.away)}</h3>
            <p>${datum(m.startTime)} · ${esc(m.arena)}</p>
            <p>${m.ticketsForSale > 0 ? `${m.ticketsForSale} biljetter från ${kronor(m.lowestPrice)}` : "Inga biljetter ute än"}</p>
        </a>`).join("");
}

// Fill the city dropdown with the cities that have upcoming matches.
async function laddaStader() {
    const stader = await api("/api/cities");
    document.getElementById("sok-stad").innerHTML = `<option value="">Alla städer</option>`
        + stader.map(s => `<option value="${esc(s)}">${esc(s)}</option>`).join("");
}

// Search when the user presses "Sök" or Enter.
document.getElementById("sok-form").addEventListener("submit", (e) => {
    e.preventDefault(); // stop the browser from reloading the page
    filter.q = document.getElementById("sok-text").value.trim();
    filter.city = document.getElementById("sok-stad").value;
    visaMatcher();
});

// Changing city searches straight away.
document.getElementById("sok-stad").addEventListener("change", () => {
    document.getElementById("sok-form").requestSubmit();
});

// Search while typing, but wait until the user pauses for 300 ms.
let vantar;
document.getElementById("sok-text").addEventListener("input", () => {
    clearTimeout(vantar);
    vantar = setTimeout(() => document.getElementById("sok-form").requestSubmit(), 300);
});

document.querySelectorAll(".välj").forEach((val) => {
    val.addEventListener("click", () => {
        document.querySelectorAll(".välj").forEach((knapp) => knapp.classList.remove("vald"));
        val.classList.add("vald");
        filter.sport = val.textContent.trim();
        visaMatcher();
    });
});

// If the user is already logged in, "Logga in" becomes "Mina sidor".
async function visaInloggning() {
    try {
        await api("/api/auth/me"); // answers 401 if not logged in, and then api() throws
        const knapp = document.getElementById("login-knapp");
        knapp.textContent = "Mina sidor";
        knapp.href = "mina-sidor.html";
    } catch {
        // Not logged in: keep the "Logga in" button as it is.
    }
}

visaInloggning();
laddaStader();
visaMatcher();
