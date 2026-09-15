import { PlanEditor } from "@/features/plans/plan-editor";
export default async function EditPlanPage({ params }: { params: Promise<{ id: string }> }) { const { id } = await params; return <PlanEditor planId={id}/>; }
