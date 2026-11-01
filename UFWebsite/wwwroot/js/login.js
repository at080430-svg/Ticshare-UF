const [loginKnapp, skapaKnapp] = document.querySelectorAll(".skicka-knapp");

function visaFel(panel, text) {
    let ruta = panel.querySelector(".felmeddelande");
    if (!ruta) {
        ruta = document.createElement("p");
        ruta.className = "felmeddelande";
        panel.querySelector(".skicka-knapp").before(ruta);
    }
    ruta.textContent = text;
}

loginKnapp.addEventListener("click", async () => {
    const panel = document.querySelector(".panel-logga-in");
    try {
        await api("/api/auth/login", "POST", {
            email: document.getElementById("inloggning-epost").value,
            password: document.getElementById("inloggning-losenord").value,
        });
        location.href = "mina-sidor.html";
    } catch {
        visaFel(panel, "Fel e-post eller lösenord.");
    }
});

skapaKnapp.addEventListener("click", async () => {
    const panel = document.querySelector(".panel-skapa-konto");
    try {
        await api("/api/auth/register", "POST", {
            fullName: document.getElementById("konto-namn").value,
            email: document.getElementById("konto-epost").value,
            password: document.getElementById("konto-losenord").value,
        });
        location.href = "mina-sidor.html";
    } catch (fel) {
        visaFel(panel, fel.message);
    }
});
