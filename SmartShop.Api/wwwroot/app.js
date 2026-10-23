const REPORT_ENDPOINT = "/api/reports/reviews";

const reportBody = document.getElementById("reportBody");
const statusBox = document.getElementById("status");
const refreshBtn = document.getElementById("refreshBtn");

function showStatus(message) {
  statusBox.textContent = message;
  statusBox.hidden = false;
}

function hideStatus() {
  statusBox.hidden = true;
}

function renderEmptyRow(message) {
  reportBody.innerHTML = `<tr><td colspan="7" class="empty">${message}</td></tr>`;
}

function renderRows(rows) {
  if (!rows.length) {
    renderEmptyRow("No review data yet.");
    return;
  }

  reportBody.innerHTML = rows
    .map(
      (row) => `
      <tr>
        <td>${row.no}</td>
        <td>${escapeHtml(row.productName)}</td>
        <td>${row.totalReviews}</td>
        <td><span class="badge positive">${row.positive}</span></td>
        <td><span class="badge negative">${row.negative}</span></td>
        <td><span class="badge neutral">${row.neutral}</span></td>
        <td class="rating">${row.averageRating.toFixed(1)}</td>
      </tr>`
    )
    .join("");
}

function escapeHtml(value) {
  const div = document.createElement("div");
  div.textContent = value ?? "";
  return div.innerHTML;
}

async function loadReport() {
  hideStatus();
  refreshBtn.disabled = true;
  renderEmptyRow("Loading...");

  try {
    const response = await fetch(REPORT_ENDPOINT);
    if (!response.ok) {
      throw new Error(`Request failed with status ${response.status}`);
    }
    const rows = await response.json();
    renderRows(rows);
  } catch (err) {
    renderEmptyRow("Failed to load data.");
    showStatus(`Could not load the review report: ${err.message}`);
  } finally {
    refreshBtn.disabled = false;
  }
}

refreshBtn.addEventListener("click", loadReport);
loadReport();
