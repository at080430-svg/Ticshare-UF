document.querySelectorAll(".välj").forEach((val) => {
    val.addEventListener("click", () => {
        document.querySelectorAll(".välj").forEach((knapp) => {
            knapp.classList.remove("vald");
        });

        val.classList.add("vald");
    });
});