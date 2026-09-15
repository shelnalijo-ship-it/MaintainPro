import { MachineForm } from "@/features/machines/machine-form";
export default async function EditMachinePage({ params }: { params: Promise<{ id: string }> }) { const { id } = await params; return <MachineForm machineId={id} />; }
