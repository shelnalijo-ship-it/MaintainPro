import { WorkOrderDetailView } from "@/features/work-orders/work-order-detail";
export default async function WorkOrderDetailPage({params}:{params:Promise<{id:string}>}){const {id}=await params;return <WorkOrderDetailView id={id}/>}
