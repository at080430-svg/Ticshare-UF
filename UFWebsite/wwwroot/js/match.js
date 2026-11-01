const matchId = new URLSearchParams(location.search).get("id");

async function visaMatch() {
    const m = await api(`/api/matches/${matchId}`);
    document.getElementById("rubrik").textContent = `${m.home} – ${m.away}`;
    document.getElementById("info").textContent = `${datum(m.startTime)} · ${m.arena} · ${m.competition ?? ""}`;

    const box = document.getElementById("biljetter");
    if (m.tickets.length === 0) {
        box.innerHTML = "<p>Inga biljetter till salu just nu.</p>";
        return;
    }
    box.innerHTML = m.tickets.map(t => `
        <div class="biljett">
            <p>Sektion ${esc(t.section)}, rad ${esc(t.row)}, plats ${esc(t.seat)}</p>
            <p>${kronor(t.price)} + ${kronor(m.serviceFee)} serviceavgift</p>
            <button onclick="kop(${t.id})">Köp</button>
        </div>`).join("");
}

async function kop(listingId) {
    try {
        const order = await api("/api/orders", "POST", { listingId });
        document.getElementById("belopp").textContent = kronor(order.totalAmount);
        document.getElementById("swishnummer").textContent = order.swishNumber;
        document.getElementById("referens").textContent = order.reference;
        document.getElementById("deadline").textContent = new Date(order.payBefore).toLocaleTimeString("sv-SE", { hour: "2-digit", minute: "2-digit" });
        document.getElementById("betalning").hidden = false;
        document.getElementById("biljetter").hidden = true;

        // The simulate endpoint only exists while developing; on the live site this returns 404.
        const knapp = document.getElementById("simulera");
        knapp.hidden = location.hostname !== "localhost";
        knapp.onclick = async () => {
            await api(`/api/orders/${order.id}/simulate-payment`, "POST");
            location.href = "mina-sidor.html";
        };
    } catch (fel) {
        if (fel.message.includes("logga in")) location.href = "login.html";
        else { alert(fel.message); visaMatch(); }
    }
}

visaMatch();
