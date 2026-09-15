import { fireEvent, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { api } from "@/lib/api-client";
import { jsonResponse, renderWithProviders } from "@/test/test-utils";
import { ReportsView } from "./reports-view";

vi.mock("@/features/auth/auth-provider", () => ({ useAuth: () => ({ hasRole: () => true }) }));
const reportPage={items:[{workOrderId:"w1",workOrderNumber:"WO-100",machineCode:"MC-1",planName:"Monthly PM",plannedDate:"2026-09-01",dueDate:"2026-09-02",technician:"Anu",supervisor:"Ravi",lifecycleStatus:"APPROVED",daysOverdue:0}],page:1,pageSize:20,totalCount:1,totalPages:1};
function mockApi(){return vi.spyOn(globalThis,"fetch").mockImplementation((input)=>{const url=String(input);if(url.includes("/departments")||url.includes("/locations")||url.includes("/machine-categories")||url.includes("/users/"))return jsonResponse([]);if(url.includes("/machines?"))return jsonResponse({items:[],page:1,pageSize:100,totalCount:0,totalPages:0});if(url.includes("/reports/preventive-maintenance"))return jsonResponse(reportPage);return jsonResponse({items:[],page:1,pageSize:20,totalCount:0,totalPages:0})})}
describe("ReportsView",()=>{beforeEach(()=>{vi.restoreAllMocks()});it("renders report rows and applies date filters",async()=>{const fetchMock=mockApi();renderWithProviders(<ReportsView/>);expect(await screen.findByText("WO-100")).toBeInTheDocument();fireEvent.change(screen.getByLabelText("From date"),{target:{value:"2026-09-01"}});await waitFor(()=>expect(fetchMock.mock.calls.some(([url])=>String(url).includes("From=2026-09-01"))).toBe(true))});it("uses the centralized file download for exports",async()=>{mockApi();const download=vi.spyOn(api,"download").mockResolvedValue(undefined);renderWithProviders(<ReportsView/>);await screen.findByText("WO-100");await userEvent.click(screen.getByRole("button",{name:/pdf/i}));expect(download).toHaveBeenCalledWith(expect.stringContaining("/reports/preventive-maintenance/export/pdf"),"preventive-maintenance.pdf")})});
