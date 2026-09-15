import { MachineDetail } from "@/features/machines/machine-detail";
export default async function MachineDetailPage({ params }: { params: Promise<{ id: string }> }) { const { id } = await params; return <MachineDetail id={id} />; }
