import { FileClock } from "lucide-react";
import { Card, EmptyState, PageHeader } from "@/components/ui";

export default function AuditLogsPage(){return <div className="page-stack"><PageHeader eyebrow="Administration" title="Audit logs" description="Review changes to protected operational records."/><Card><EmptyState icon={<FileClock size={34}/>} title="Audit API not available" description="The backend records audit events but does not expose a read endpoint. This screen is ready for the timestamp, user, action, entity, and old/new-value view once that contract is added."/></Card></div>}
